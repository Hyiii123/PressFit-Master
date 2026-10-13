using PressFit.Core.Enums;
using PressFit.Core.Models;

namespace PressFit.Core.Interfaces;

/// <summary>
/// 工业 ISA-18.2 报警生命周期管理服务接口
/// </summary>
public interface IAlarmManagerService
{
    IReadOnlyList<AlarmRecord> ActiveAlarms { get; }
    int UnacknowledgedCount { get; }
    AlarmRecord? HighestPriorityActiveAlarm { get; }

    event Action? OnAlarmsChanged;

    Task RaiseAlarmAsync(string code, string description, AlarmLevel level);
    Task AcknowledgeAlarmAsync(long alarmId);
    Task AcknowledgeAllAlarmsAsync();
    Task ClearAlarmAsync(string code);
}
