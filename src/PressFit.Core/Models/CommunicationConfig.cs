using PressFit.Core.Enums;

namespace PressFit.Core.Models;

/// <summary>
/// 工业上位机通信网络参数动态配置模型
/// 支持 Modbus TCP 与西门子 S7-1200/1500 协议参数切换
/// </summary>
public class CommunicationConfig
{
    public PlcProtocolType Protocol { get; set; } = PlcProtocolType.ModbusTcp; // 当前选定协议
    public string IpAddress { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 5020;
    public int PollingIntervalMs { get; set; } = 20;     // 默认 50Hz (20ms) 高频采集
    public int TimeoutMs { get; set; } = 2000;           // Socket 通信超时时间 (ms)
    public int HeartbeatIntervalMs { get; set; } = 500;  // 看门狗心跳保活间隔 (ms)
    public bool AutoReconnect { get; set; } = true;      // 是否开启指数退避自动重连

    // 西门子 S7 专有参数 (S7-1200/1500 机架号通常为0，槽位号为1；S7-300为2)
    public int SiemensRack { get; set; } = 0;
    public int SiemensSlot { get; set; } = 1;
    public int SiemensDbNumber { get; set; } = 1; // 默认数据块 DB1
    public DateTime LastUpdatedAt { get; set; } = DateTime.Now;
}
