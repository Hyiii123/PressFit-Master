using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PressFit.Communication.Modbus;
using PressFit.Communication.Pipeline;
using PressFit.Communication.StateMachine;
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
    private readonly ConnectionWatchdog _watchdog;

    // 通信连接属性
    [ObservableProperty] private string _serverIp = "127.0.0.1";
    [ObservableProperty] private int _serverPort = 5020;
    [ObservableProperty] private bool _isConnected = false;
    [ObservableProperty] private string _connectionStatusText = "未连接 (离线)";

    // 设备与时序状态
    [ObservableProperty] private string _machineStateText = "待机中 (Idle)";
    [ObservableProperty] private string _pressPhaseText = "原位就绪";
    [ObservableProperty] private double _currentDisplacement = 0.0;
    [ObservableProperty] private double _currentForce = 0.0;
    [ObservableProperty] private double _peakForce = 0.0;
    [ObservableProperty] private double _cycleTimeMs = 0.0;

    // 工业多级权限与审计
    [ObservableProperty] private UserRole _currentUserRole = UserRole.Operator;
    [ObservableProperty] private string _currentUserRoleText = "产线操作员 (Operator)";
    [ObservableProperty] private bool _canEditRecipe = false;

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

    // 仿真器与故障注入
    [ObservableProperty] private bool _isSimulatorRunning = false;
    [ObservableProperty] private bool _faultDisconnect = false;
    [ObservableProperty] private bool _faultTimeout = false;
    [ObservableProperty] private bool _faultForceSpike = false;
    [ObservableProperty] private bool _faultForceLow = false;
    [ObservableProperty] private bool _faultDispShort = false;
    [ObservableProperty] private bool _faultServoAlarm = false;

    // 运行日志流水
    public ObservableCollection<string> LogMessages { get; } = new();

    // 采集缓存点
    public List<PressPoint> CurrentCyclePoints { get; } = new();
    public event Action<IReadOnlyList<PressPoint>>? OnBatchDataReceived;
    public event Action? OnCycleResetRequested;

    public MainViewModel(
        IModbusMasterService modbusClient,
        IPressFitRepository repository,
        IPressDataPipeline pipeline,
        IVirtualSlaveService simulator)
    {
        _modbusClient = modbusClient;
        _repository = repository;
        _pipeline = pipeline;
        _simulator = simulator;

        _watchdog = new ConnectionWatchdog(_modbusClient);
        _watchdog.StatusChanged += Watchdog_StatusChanged;
        _watchdog.LogMessageReceived += msg => AddLog(msg);

        // 默认自动启动内置仿真器，实现开箱即用一键演示
        ToggleSimulatorCommand.Execute(null);
        LoadRecipesAsync();
        StartAcquisitionLoop();
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
        Application.Current?.Dispatcher.Invoke(() =>
        {
            IsConnected = status == ConnectionStatus.Connected;
            ConnectionStatusText = status switch
            {
                ConnectionStatus.Connected => "通信正常 (在线)",
                ConnectionStatus.Connecting => "握手重连中...",
                ConnectionStatus.Degraded => "通信降级 (重试中)",
                _ => "未连接 (离线)"
            };
        });
    }

    [RelayCommand]
    private void Connect()
    {
        AddLog($"手动触发连接 -> {ServerIp}:{ServerPort}");
        _watchdog.Start(ServerIp, ServerPort);
    }

    [RelayCommand]
    private async Task DisconnectAsync()
    {
        _watchdog.Stop();
        await _modbusClient.DisconnectAsync();
        IsConnected = false;
        ConnectionStatusText = "已手动断开";
        AddLog("已断开 Modbus 连接");
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
            AddLog($"[权限审计] 系统操作身份已切换为: {CurrentUserRoleText}");
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
            _simulator.StartAsync(ServerPort);
            IsSimulatorRunning = true;
            AddLog($"内置虚拟仿真器已启动，监听端口: {ServerPort}");
            Connect();
        }
    }

    [RelayCommand]
    private void ApplyFaults()
    {
        _simulator.InjectFault("Disconnect", FaultDisconnect);
        _simulator.InjectFault("Timeout", FaultTimeout);
        _simulator.InjectFault("ForceSpike", FaultForceSpike);
        _simulator.InjectFault("ForceLow", FaultForceLow);
        _simulator.InjectFault("DispShort", FaultDispShort);
        _simulator.InjectFault("ServoAlarm", FaultServoAlarm);
        AddLog("[故障注入] 故障开关配置已同步至仿真引擎");
    }

    private void StartAcquisitionLoop()
    {
        // 1. 高频网络轮询任务 (50Hz)
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
                await Task.Delay(20); // 50Hz 轮询间隔
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

        QualityResultText = eval.IsPass ? "PASS (OK)" : $"NG: {eval.Status}";
        IsQualityOk = eval.IsPass;

        AddLog($"[周期结束] 判定结果: {QualityResultText}, 峰值压力: {eval.PeakForce:F2}kN, 终点位移: {eval.FinalDisplacement:F2}mm");
        if (!eval.IsPass)
        {
            AddLog($"[NG详情] {eval.EvaluationMessage}");
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
            CreatedAt = DateTime.Now
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
