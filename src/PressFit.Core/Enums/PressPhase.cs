namespace PressFit.Core.Enums;

/// <summary>
/// 伺服压装单次工艺时序阶段
/// </summary>
public enum PressPhase
{
    Standby = 0,         // 原位待命
    RapidApproach = 1,   // 快速下行接近工件
    ContactDetect = 2,   // 接触工件（压力触发判定）
    ControlledPress = 3, // 恒速可控压入段（高频特征点采集）
    ForceHold = 4,       // 保压维持阶段
    Retracting = 5,      // 回退至安全原位
    Completed = 6        // 周期结束判定
}
