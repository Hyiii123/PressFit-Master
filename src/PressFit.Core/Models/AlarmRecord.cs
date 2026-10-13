using PressFit.Core.Enums;

namespace PressFit.Core.Models;

/// <summary>
/// 遵循 ISA-18.2 工业标准的设备报警生命周期记录
/// </summary>
public class AlarmRecord
{
    public long Id { get; set; }
    public string AlarmCode { get; set; } = string.Empty;       // 报警代码 (如 ALM-COMM-001, ALM-SERVO-5001)
    public string Description { get; set; } = string.Empty;     // 报警描述文本
    public AlarmLevel Level { get; set; } = AlarmLevel.Warning; // 级别 (Info, Warning, Error, Fatal)
    public DateTime OccurredAt { get; set; } = DateTime.Now;    // 触发时间
    public DateTime? AcknowledgedAt { get; set; }               // 人工确认时间 (Acknowledge)
    public DateTime? ClearedAt { get; set; }                    // 物理消除/恢复时间 (Cleared)

    public bool IsAcknowledged => AcknowledgedAt.HasValue;
    public bool IsCleared => ClearedAt.HasValue;
    public bool IsActive => !IsCleared;                         // 是否处于当前活动报警中

    /// <summary>
    /// ISA-18.2 状态判定文本
    /// </summary>
    public string StateText
    {
        get
        {
            if (IsActive && !IsAcknowledged) return "活动·未确认 (Active Unack)";
            if (IsActive && IsAcknowledged) return "活动·已确认 (Active Ack)";
            if (!IsActive && !IsAcknowledged) return "消除·未确认 (Cleared Unack)";
            return "已恢复归档 (Normal)";
        }
    }
}
