namespace PressFit.Core.Enums;

/// <summary>
/// 压装机整机运行状态机
/// </summary>
public enum MachineState
{
    Disconnected = 0, // 未连接/通信离线
    Offline = 1,      // 离线
    Idle = 2,         // 待机中（准备就绪）
    Running = 3,      // 压装运行中
    Paused = 4,       // 暂停中
    Alarm = 5,        // 故障停机/报警
    EmergencyStop = 6 // 急停状态
}
