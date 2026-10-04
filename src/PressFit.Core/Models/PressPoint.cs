namespace PressFit.Core.Models;

/// <summary>
/// 压装过程高频采样点 (Displacement, Force, Timestamp)
/// </summary>
public readonly record struct PressPoint(
    double Displacement, // 当前位移 (mm)
    double Force,        // 实时压力 (kN)
    double TimestampMs   // 相对于压装起点的毫秒偏移 (ms)
);
