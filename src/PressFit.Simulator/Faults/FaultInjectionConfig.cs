namespace PressFit.Simulator.Faults;

/// <summary>
/// 虚拟下位机故障注入配置
/// </summary>
public class FaultInjectionConfig
{
    public bool SimulateNetworkDisconnect { get; set; } = false; // 模拟网络电缆断开
    public bool SimulatePacketTimeout { get; set; } = false;    // 模拟通信响应严重超时 (>3s)
    public bool SimulateForceSpikeNG { get; set; } = false;     // 模拟过盈卡滞压力超限 (NG)
    public bool SimulateForceLowNG { get; set; } = false;       // 模拟配合过松压力偏低 (NG)
    public bool SimulateDisplacementShortNG { get; set; } = false; // 模拟未压到位提前停机 (NG)
    public bool SimulateServoAlarm { get; set; } = false;       // 模拟伺服驱动器过载报警
}
