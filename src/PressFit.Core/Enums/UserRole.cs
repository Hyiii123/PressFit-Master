namespace PressFit.Core.Enums;

/// <summary>
/// 工业现场三级用户身份权限
/// </summary>
public enum UserRole
{
    Operator = 0,     // 产线操作员：仅支持扫码、批次切换、启动、急停
    Engineer = 1,     // 工艺工程师：支持配方编辑、公差标定、参数下发、曲线复盘
    Administrator = 2 // 系统管理员：最高权限，支持通信参数修改、数据库维护
}
