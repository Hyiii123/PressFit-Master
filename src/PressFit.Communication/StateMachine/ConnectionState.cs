namespace PressFit.Communication.StateMachine;

/// <summary>
/// 通信连接生命周期状态机
/// </summary>
public enum ConnectionStatus
{
    Disconnected = 0, // 完全断开
    Connecting = 1,   // 正在握手重连中
    Connected = 2,    // 正常在线通讯
    Degraded = 3      // 降级模式（丢包重试中）
}
