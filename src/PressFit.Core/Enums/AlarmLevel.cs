namespace PressFit.Core.Enums;

/// <summary>
/// 报警严重程度等级
/// </summary>
public enum AlarmLevel
{
    Info = 0,    // 提示信息
    Warning = 1, // 警告（不影响动作）
    Error = 2,   // 严重错误（停机）
    Fatal = 3    // 致命急停
}
