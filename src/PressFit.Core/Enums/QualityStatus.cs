namespace PressFit.Core.Enums;

/// <summary>
/// 压装结果品质判定状态
/// </summary>
public enum QualityStatus
{
    Unknown = 0,
    OK = 1,               // 合格品
    NG_ForceExceeded = 2, // 压力超上限（过盈卡滞）
    NG_ForceTooLow = 3,   // 压力未达下限（虚压/孔径过大）
    NG_Displacement = 4,  // 终点位移未达标或超程
    NG_EnvelopeFault = 5, // 过程轨迹穿透包络线公差带
    NG_CycleTimeout = 6   // 压装超时异常
}
