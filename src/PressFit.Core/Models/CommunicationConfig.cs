namespace PressFit.Core.Models;

/// <summary>
/// 工业上位机通信网络参数动态配置模型
/// </summary>
public class CommunicationConfig
{
    public string IpAddress { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 5020;
    public int PollingIntervalMs { get; set; } = 20;     // 默认 50Hz (20ms) 高频采集
    public int TimeoutMs { get; set; } = 2000;           // Socket 通信超时时间 (ms)
    public int HeartbeatIntervalMs { get; set; } = 500;  // 看门狗心跳保活间隔 (ms)
    public bool AutoReconnect { get; set; } = true;      // 是否开启指数退避自动重连
    public DateTime LastUpdatedAt { get; set; } = DateTime.Now;
}
