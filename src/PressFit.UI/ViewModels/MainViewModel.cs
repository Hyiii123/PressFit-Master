using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PressFit.Communication.Config;
using PressFit.Communication.Modbus;
using PressFit.Communication.Pipeline;
using PressFit.Communication.StateMachine;
using PressFit.Communication.StoreAndForward;
using PressFit.Core.Algorithms;
using PressFit.Core.Enums;
using PressFit.Core.Interfaces;
using PressFit.Core.Models;
using PressFit.Simulator;

namespace PressFit.UI.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IModbusMasterService _modbusClient;
    private readonly IPressFitRepository _repository;
    private readonly IPressDataPipeline _pipeline;
    private readonly IVirtualSlaveService _simulator;
    private readonly IAlarmManagerService _alarmService;
    private readonly ConnectionWatchdog _watchdog;
    private readonly MesSyncWorker _mesSyncWorker;

    // 通信网络参数配置
    [ObservableProperty] private CommunicationConfig _commConfig = new();
    [ObservableProperty] private bool _isConnected = false;
    [ObservableProperty] private string _connectionStatusText = "未连接 (离线)";

    // 设备与时序状态
    [ObservableProperty] private string _machineStateText = "待机中 (Idle)";
    [ObservableProperty] private string _pressPhaseText = "原位就绪";
    [ObservableProperty] private double _currentDisplacement = 0.0;
    [ObservableProperty] private double _currentForce = 0.0;
    [ObservableProperty] private double _peakForce = 0.0;
    [ObservableProperty] private double _cycleTimeMs = 0.0;

    // 工业 ISA-18.2 实时报警横幅
    [ObservableProperty] private bool _hasActiveAlarm = false;
    [ObservableProperty] private string _activeAlarmBannerText = "当前无报警";
    [ObservableProperty] private int _unacknowledgedAlarmCount = 0;

    // 工业多级权限与审计
    [ObservableProperty] private UserRole _currentUserRole = UserRole.Operator;
    [ObservableProperty] private string _currentUserRoleText = "产线操作员 (Operator)";
    [ObservableProperty] private bool _canEditRecipe = false;
    [ObservableProperty] private bool _canConfigure = false;

    // 多语言国际化 (i18n)
    [ObservableProperty] private string _currentLanguage = "zh-CN";
    [ObservableProperty] private string _langToggleText = "🌐 EN";

    // 边缘断点补传 (Store-and-Forward)
    [ObservableProperty] private int _pendingSyncCount = 0;
    [ObservableProperty] private string _syncStatusText = "云端同步: 实时在线";

    // 工艺配方与工件
    [ObservableProperty] private ObservableCollection<PressRecipe> _recipes = new();
    [ObservableProperty] private PressRecipe? _selectedRecipe;
    [ObservableProperty] private string _workpieceBarcode = "SN-MOTOR-20261004-001";

    // 判定结果与统计 KPI
    [ObservableProperty] private string _qualityResultText = "WAITING";
    [ObservableProperty] private bool? _isQualityOk = null;
    [ObservableProperty] private int _totalCount = 0;
    [ObservableProperty] private int _okCount = 0;
    [ObservableProperty] private int _ngCount = 0;
    [ObservableProperty] private double _yieldRate = 100.0;

    // 精益生产 OEE 与经济产值指标
    [ObservableProperty] private double _oeePercentage = 100.0;
    [ObservableProperty] private double _totalOutputValue = 0.0;
    [ObservableProperty] private double _totalScrapLoss = 0.0;
    [ObservableProperty] private double _netEconomicValue = 0.0;
    [ObservableProperty] private string _oeeGradeText = "世界级 (World Class)";

    // 仿真器与故障注入
    [ObservableProperty] private bool _isSimulatorRunning = false;
    [ObservableProperty] private bool _faultDisconnect = false;
    [ObservableProperty] private bool _faultTimeout = false;
    [ObservableProperty] private bool _faultForceSpike = false;
    [ObservableProperty] private bool _faultForceLow = false;
    [ObservableProperty] private bool _faultDispShort = false;
    [ObservableProperty] private bool _faultServoAlarm = false;

    // 运行日志与底层报文抓包流水
    public ObservableCollection<string> LogMessages { get; } = new();
    public ObservableCollection<string> RawFrameLogs { get; } = new();

    // 采集缓存点
    public List<PressPoint> CurrentCyclePoints { get; } = new();
    public event Action<IReadOnlyList<PressPoint>>? OnBatchDataReceived;
    public event Action? OnCycleResetRequested;

    public MainViewModel(
        IModbusMasterService modbusClient,
        IPressFitRepository repository,
        IPressDataPipeline pipeline,
        IVirtualSlaveService simulator,
        IAlarmManagerService alarmService)
    {
        _modbusClient = modbusClient;
        _repository = repository;
        _pipeline = pipeline;
        _simulator = simulator;
        _alarmService = alarmService;

        CommConfig = CommunicationConfigManager.LoadConfig();

        _watchdog = new ConnectionWatchdog(_modbusClient);
        _watchdog.StatusChanged += Watchdog_StatusChanged;
        _watchdog.LogMessageReceived += msg => AddLog(msg);
        _modbusClient.OnRawFrameLogged += OnRawFrameReceived;
        _alarmService.OnAlarmsChanged += RefreshAlarmBanner;

        // 启动边缘离线断点续传工作引擎
        _mesSyncWorker = new MesSyncWorker(_repository);
        _mesSyncWorker.OnSyncProgressChanged += (pending, synced) =>
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                PendingSyncCount = pending;
                SyncStatusText = pending > 0 ? $"云端同步: 离线暂存 {pending} 件补传中" : "云端同步: 实时在线已同步";
            });
        };
        _mesSyncWorker.Start();

        // 默认自动启动内置仿真器，实现开箱即用一键演示
        ToggleSimulatorCommand.Execute(null);
        LoadRecipesAsync();
        StartAcquisitionLoop();
    }

    private void RefreshAlarmBanner()
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            var top = _alarmService.HighestPriorityActiveAlarm;
            HasActiveAlarm = top != null;
            UnacknowledgedAlarmCount = _alarmService.UnacknowledgedCount;
            if (top != null)
            {
                ActiveAlarmBannerText = $"🚨 [{top.Level}] {top.AlarmCode} : {top.Description} (触发于 {top.OccurredAt:HH:mm:ss})";
            }
            else
            {
                ActiveAlarmBannerText = "系统运行正常，无活动报警";
            }
        });
    }

    private void OnRawFrameReceived(RawFrameLog log)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            string dir = log.IsTx ? ">> [TX 发送]" : "<< [RX 接收]";
            string item = $"{log.Timestamp:HH:mm:ss.fff} {dir} {log.HexString} ({log.Description})";
            RawFrameLogs.Insert(0, item);
            if (RawFrameLogs.Count > 60) RawFrameLogs.RemoveAt(RawFrameLogs.Count - 1);
        });
    }

    public async void LoadRecipesAsync()
    {
        var list = await _repository.GetAllRecipesAsync();
        Recipes.Clear();
        foreach (var r in list) Recipes.Add(r);
        SelectedRecipe = Recipes.FirstOrDefault(r => r.RecipeCode == SelectedRecipe?.RecipeCode) ?? Recipes.FirstOrDefault();
    }

    private void Watchdog_StatusChanged(ConnectionStatus status)
    {
        Application.Current?.Dispatcher.Invoke(async () =>
        {
            IsConnected = status == ConnectionStatus.Connected;
            ConnectionStatusText = status switch
            {
                ConnectionStatus.Connected => "通信正常 (在线)",
                ConnectionStatus.Connecting => "握手重连中...",
                ConnectionStatus.Degraded => "通信降级 (重试中)",
                _ => "未连接 (离线)"
            };

            if (status == ConnectionStatus.Connected)
            {
                _mesSyncWorker.IsOnline = true;
                await _alarmService.ClearAlarmAsync("ALM-COMM-001");
            }
            else if (status == ConnectionStatus.Disconnected || status == ConnectionStatus.Degraded)
            {
                _mesSyncWorker.IsOnline = false;
                await _alarmService.RaiseAlarmAsync("ALM-COMM-001", "下位机以太网通信中断，自动看门狗正在执行退避重连", AlarmLevel.Warning);
            }
        });
    }

    [RelayCommand]
    private void ToggleLanguage()
    {
        if (CurrentLanguage == "zh-CN")
        {
            CurrentLanguage = "en-US";
            LangToggleText = "🌐 中文";
        }
        else
        {
            CurrentLanguage = "zh-CN";
            LangToggleText = "🌐 EN";
        }
        App.SwitchLanguage(CurrentLanguage);
        AddLog($"[系统语言] 全局语言包已切换为: {CurrentLanguage}");
    }

    [RelayCommand]
    private void Connect()
    {
        AddLog($"手动触发连接 -> {CommConfig.IpAddress}:{CommConfig.Port} [{CommConfig.Protocol}]");
        _watchdog.Start(CommConfig.IpAddress, CommConfig.Port);
    }

    [RelayCommand]
    private async Task DisconnectAsync()
    {
        _watchdog.Stop();
        await _modbusClient.DisconnectAsync();
        IsConnected = false;
        ConnectionStatusText = "已手动断开";
        AddLog("已断开通信链路");
    }

    [RelayCommand]
    private void SwitchRole()
    {
        var dialog = new Views.UserLoginDialog();
        dialog.Owner = Application.Current?.MainWindow;
        if (dialog.ShowDialog() == true)
        {
            CurrentUserRole = dialog.SelectedRole;
            CurrentUserRoleText = dialog.SelectedRole switch
            {
                UserRole.Engineer => "工艺工程师 (Engineer)",
                UserRole.Administrator => "系统管理员 (Administrator)",
                _ => "产线操作员 (Operator)"
            };
            CanEditRecipe = CurrentUserRole != UserRole.Operator;
            CanConfigure = CurrentUserRole == UserRole.Administrator || CurrentUserRole == UserRole.Engineer;
            AddLog($"[权限审计] 系统操作身份已切换为: {CurrentUserRoleText}");
        }
    }

    [RelayCommand]
    private void OpenCommunicationConfig()
    {
        if (!CanConfigure)
        {
            MessageBox.Show("当前处于【产线操作员】身份，无权修改现场底层网络通信参数！\n请点击右上角【切换身份】登录工程师或管理员账号。", "权限防呆提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var win = new Views.CommunicationConfigWindow(CommConfig);
        win.Owner = Application.Current?.MainWindow;
        if (win.ShowDialog() == true && win.HasApplied)
        {
            CommConfig = win.Config;
            AddLog($"[通信配置] 通信参数已热更新 -> {CommConfig.IpAddress}:{CommConfig.Port} [{CommConfig.Protocol}]");
            Connect();
        }
    }

    [RelayCommand]
    private void OpenAlarmCenter()
    {
        var win = new Views.AlarmCenterWindow(_alarmService, _repository);
        win.Owner = Application.Current?.MainWindow;
        win.ShowDialog();
    }

    [RelayCommand]
    private async Task AcknowledgeTopAlarmAsync()
    {
        var top = _alarmService.HighestPriorityActiveAlarm;
        if (top != null)
        {
            await _alarmService.AcknowledgeAlarmAsync(top.Id);
            AddLog($"[报警确认] 已人工确认报警: {top.AlarmCode}");
        }
    }

    [RelayCommand]
    private void OpenRecipeManager()
    {
        if (!CanEditRecipe)
        {
            MessageBox.Show("当前处于【产线操作员】身份，无权修改工艺配方与下发参数！\n请点击右上角【切换身份】输入工程师密码 (1234)。", "权限防呆提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var win = new Views.RecipeManagerWindow(_repository, _modbusClient);
        win.Owner = Application.Current?.MainWindow;
        win.OnRecipesChanged += () => LoadRecipesAsync();
        win.ShowDialog();
    }

    [RelayCommand]
    private void OpenHistoryQuery()
    {
        var win = new Views.HistoryQueryWindow(_repository);
        win.Owner = Application.Current?.MainWindow;
        win.ShowDialog();
    }

    [RelayCommand]
    private void OpenSpcAnalysis()
    {
        var win = new Views.SpcAnalysisWindow(_repository);
        win.Owner = Application.Current?.MainWindow;
        win.ShowDialog();
    }

    [RelayCommand]
    private async Task StartCycleAsync()
    {
        if (!IsConnected)
        {
            AddLog("[错误] 未建立通信连接，无法启动压装");
            return;
        }

        if (SelectedRecipe == null)
        {
            AddLog("[错误] 请先选择工艺配方");
            return;
        }

        CurrentCyclePoints.Clear();
        OnCycleResetRequested?.Invoke();
        QualityResultText = "PRESSING...";
        IsQualityOk = null;

        AddLog($"下发配方 [{SelectedRecipe.RecipeCode}] 并触发压装，工件条码: {WorkpieceBarcode}");

        // 下发启动线圈
        await _modbusClient.WriteSingleCoilAsync(1, ModbusAddressMap.COIL_CMD_START, true);
    }

    [RelayCommand]
    private async Task EmergencyStopAsync()
    {
        AddLog("[警告] 触发紧急停机 (E-STOP) 指令！");
        if (IsConnected)
        {
            await _modbusClient.WriteSingleCoilAsync(1, ModbusAddressMap.COIL_CMD_STOP, true);
        }
        _simulator.TriggerEmergencyStop();
        await _alarmService.RaiseAlarmAsync("ALM-E-STOP", "紧急停止按钮被操作工拍下！现场伺服动力已切断", AlarmLevel.Fatal);
    }

    [RelayCommand]
    private void ToggleSimulator()
    {
        if (IsSimulatorRunning)
        {
            _simulator.StopAsync();
            IsSimulatorRunning = false;
            AddLog("内置虚拟仿真器已关闭");
        }
        else
        {
            _simulator.StartAsync(CommConfig.Port);
            IsSimulatorRunning = true;
            AddLog($"内置虚拟仿真器已启动，监听端口: {CommConfig.Port}");
            Connect();
        }
    }

    [RelayCommand]
    private async Task ApplyFaultsAsync()
    {
        _simulator.InjectFault("Disconnect", FaultDisconnect);
        _simulator.InjectFault("Timeout", FaultTimeout);
        _simulator.InjectFault("ForceSpike", FaultForceSpike);
        _simulator.InjectFault("ForceLow", FaultForceLow);
        _simulator.InjectFault("DispShort", FaultDispShort);
        _simulator.InjectFault("ServoAlarm", FaultServoAlarm);

        if (FaultServoAlarm)
        {
            await _alarmService.RaiseAlarmAsync("ALM-SERVO-5001", "伺服驱动器过载报警：电流超过额定阈值 150%", AlarmLevel.Error);
        }
        else
        {
            await _alarmService.ClearAlarmAsync("ALM-SERVO-5001");
        }

        AddLog("[故障注入] 故障开关配置已同步至仿真引擎");
    }

    private void StartAcquisitionLoop()
    {
        // 1. 高频网络轮询任务 (依据配置动态调节)
        Task.Run(async () =>
        {
            bool wasRunning = false;
            while (true)
            {
                if (IsConnected)
                {
                    try
                    {
                        var regs = await _modbusClient.ReadHoldingRegistersAsync(1, 0, 7);
                        var coils = await _modbusClient.ReadCoilsAsync(1, 0, 6);

                        short state = regs[ModbusAddressMap.REG_MACHINE_STATE];
                        short phase = regs[ModbusAddressMap.REG_PRESS_PHASE];
                        double disp = regs[ModbusAddressMap.REG_DISPLACEMENT_SCALED] / 100.0;
                        double force = regs[ModbusAddressMap.REG_FORCE_SCALED] / 100.0;
                        double peak = regs[ModbusAddressMap.REG_PEAK_FORCE_SCALED] / 100.0;
                        int timeMs = regs[ModbusAddressMap.REG_CYCLE_TIME_MS];
                        bool cycleDone = coils[ModbusAddressMap.COIL_STATUS_CYCLE_DONE];

                        bool isRunning = state == (short)MachineState.Running;

                        Application.Current?.Dispatcher.Invoke(() =>
                        {
                            CurrentDisplacement = disp;
                            CurrentForce = force;
                            PeakForce = peak;
                            CycleTimeMs = timeMs;
                            MachineStateText = ((MachineState)state).ToString();
                            PressPhaseText = ((PressPhase)phase).ToString();
                        });

                        if (isRunning)
                        {
                            var pt = new PressPoint(disp, force, timeMs);
                            await _pipeline.EnqueuePointAsync(pt);
                        }

                        // 检测到周期结束边沿
                        if (wasRunning && !isRunning && cycleDone)
                        {
                            await OnCycleCompletedAsync();
                        }
                        wasRunning = isRunning;
                    }
                    catch { }
                }
                int delay = Math.Max(10, CommConfig.PollingIntervalMs);
                await Task.Delay(delay);
            }
        });

        // 2. 消费流水线批次任务 (60FPS 批量交付界面与点集)
        Task.Run(async () =>
        {
            await foreach (var batch in _pipeline.ReadBatchesAsync())
            {
                lock (CurrentCyclePoints)
                {
                    CurrentCyclePoints.AddRange(batch);
                }

                Application.Current?.Dispatcher.Invoke(() =>
                {
                    OnBatchDataReceived?.Invoke(batch);
                });
            }
        });
    }

    private async Task OnCycleCompletedAsync()
    {
        if (SelectedRecipe == null) return;

        List<PressPoint> pointsCopy;
        lock (CurrentCyclePoints)
        {
            pointsCopy = CurrentCyclePoints.ToList();
        }

        // 调用核心包络线判定算法
        var eval = EnvelopeDetector.Evaluate(pointsCopy, SelectedRecipe);

        TotalCount++;
        if (eval.IsPass) OkCount++;
        else NgCount++;
        YieldRate = TotalCount > 0 ? (double)OkCount / TotalCount * 100.0 : 100.0;

        // 计算 OEE 综合设备效率与产值核算
        double plannedSec = Math.Max(60, TotalCount * 25.0);
        double opSec = plannedSec * 0.95;
        var oeeRes = OeeCalculator.Calculate(
            plannedSec, opSec, TotalCount, OkCount, 22.0,
            SelectedRecipe.UnitPrice, SelectedRecipe.ScrapCost
        );
        OeePercentage = oeeRes.OeePercentage;
        TotalOutputValue = oeeRes.TotalOutputValue;
        TotalScrapLoss = oeeRes.TotalScrapLoss;
        NetEconomicValue = oeeRes.NetEconomicValue;
        OeeGradeText = oeeRes.OeeEvaluationGrade;

        QualityResultText = eval.IsPass ? "PASS (OK)" : $"NG: {eval.Status}";
        IsQualityOk = eval.IsPass;

        AddLog($"[周期结束] 判定结果: {QualityResultText}, 峰值压力: {eval.PeakForce:F2}kN, 终点位移: {eval.FinalDisplacement:F2}mm | 当班OEE: {OeePercentage}% 累计产值: ¥{TotalOutputValue}");
        if (!eval.IsPass)
        {
            AddLog($"[NG详情] {eval.EvaluationMessage}");
            await _alarmService.RaiseAlarmAsync($"ALM-QC-NG-{eval.Status}", $"工件 {WorkpieceBarcode} 质检不合格: {eval.EvaluationMessage}", AlarmLevel.Warning);
        }

        // 异步落盘 SQLite (Dapper)
        var record = new PressResultRecord
        {
            WorkpieceBarcode = WorkpieceBarcode,
            RecipeCode = SelectedRecipe.RecipeCode,
            Quality = eval.Status,
            PeakForce = eval.PeakForce,
            FinalDisplacement = eval.FinalDisplacement,
            CycleDurationSec = CycleTimeMs / 1000.0,
            FailureReason = eval.EvaluationMessage,
            CurvePointsJson = JsonSerializer.Serialize(pointsCopy),
            CreatedAt = DateTime.Now,
            IsSyncedToMes = false
        };

        await _repository.SaveProductionRecordAsync(record);
        AddLog($"[数据归档] 生产记录已持久化至 SQLite (ID: {record.Id})");

        // 自动递增条码流水号
        WorkpieceBarcode = $"SN-MOTOR-20261004-{TotalCount + 1:D3}";
    }

    public void AddLog(string msg)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            string timeStr = DateTime.Now.ToString("HH:mm:ss.fff");
            LogMessages.Insert(0, $"[{timeStr}] {msg}");
            if (LogMessages.Count > 100) LogMessages.RemoveAt(LogMessages.Count - 1);
        });
    }
}
