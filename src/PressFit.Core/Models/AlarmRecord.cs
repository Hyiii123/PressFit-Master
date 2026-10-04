using PressFit.Core.Enums;

namespace PressFit.Core.Models;

/// <summary>
/// 工业现场设备告警记录
/// </summary>
public class AlarmRecord
{
    public long Id { get; set; }
    public string AlarmCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public AlarmLevel Level { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.Now;
    public DateTime? ClearedAt { get; set; }
    public bool IsCleared => ClearedAt.HasValue;
}
