using Microsoft.Extensions.Logging;
using PressFit.Core.Interfaces;

namespace PressFit.Communication.StateMachine;

/// <summary>
/// 工业级自愈式通信看门狗 (Connection Watchdog)
/// 具备心跳保活、指数退避重连算法（Exponential Backoff）与网络自适应恢复
/// </summary>
public class ConnectionWatchdog
{
    private readonly IModbusMasterService _modbusClient;
    private readonly ILogger<ConnectionWatchdog>? _logger;
    private CancellationTokenSource? _cts;
    private Task? _loopTask;

    private int _consecutiveFailures = 0;
    private const int MaxBackoffSeconds = 16;

    public ConnectionStatus CurrentStatus { get; private set; } = ConnectionStatus.Disconnected;
    public event Action<ConnectionStatus>? StatusChanged;
    public event Action<string>? LogMessageReceived;

    public ConnectionWatchdog(IModbusMasterService modbusClient, ILogger<ConnectionWatchdog>? logger = null)
    {
        _modbusClient = modbusClient;
        _logger = logger;
    }

    public void Start(string ip, int port)
    {
        Stop();
        _cts = new CancellationTokenSource();
        _loopTask = Task.Run(() => WatchdogLoopAsync(ip, port, _cts.Token));
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts = null;
    }

    private async Task WatchdogLoopAsync(string ip, int port, CancellationToken ct)
    {
        Log($""看门狗启动，目标从机地址: {ip}:{port}"");

        while (!ct.IsCancellationRequested)
        {
            if (!_modbusClient.IsConnected)
            {
                UpdateStatus(ConnectionStatus.Connecting);
                Log($""[重连状态机] 尝试建立连接 (当前连续失败 {_consecutiveFailures} 次)..."");

                bool success = await _modbusClient.ConnectAsync(ip, port, ct);
                if (success)
                {
                    _consecutiveFailures = 0;
                    UpdateStatus(ConnectionStatus.Connected);
                    Log(""[重连状态机] 握手成功，通信恢复在线！"");
                }
                else
                {
                    _consecutiveFailures++;
                    UpdateStatus(ConnectionStatus.Disconnected);

                    // 指数退避算法: 2^(min(failures, 4)) 秒，避免网络风暴惊群
                    int delaySec = Math.Min((int)Math.Pow(2, Math.Min(_consecutiveFailures, 4)), MaxBackoffSeconds);
                    Log($""[重连状态机] 连接失败，进入退避保护，将在 {delaySec} 秒后重试..."");
                    await Task.Delay(TimeSpan.FromSeconds(delaySec), ct);
                    continue;
                }
            }

            // 心跳检测保活：每 500ms 读取状态寄存器 0
            try
            {
                await _modbusClient.ReadHoldingRegistersAsync(1, 0, 1, ct);
                if (CurrentStatus != ConnectionStatus.Connected)
                {
                    UpdateStatus(ConnectionStatus.Connected);
                }
            }
            catch (Exception ex)
            {
                _consecutiveFailures++;
                Log($""[心跳检测] 异常失步: {ex.Message}"");
                await _modbusClient.DisconnectAsync();
                UpdateStatus(ConnectionStatus.Degraded);
            }

            await Task.Delay(500, ct);
        }
    }

    private void UpdateStatus(ConnectionStatus newStatus)
    {
        if (CurrentStatus != newStatus)
        {
            CurrentStatus = newStatus;
            StatusChanged?.Invoke(newStatus);
        }
    }

    private void Log(string msg)
    {
        LogMessageReceived?.Invoke(msg);
        _logger?.LogInformation(""{Message}"", msg);
    }
}
