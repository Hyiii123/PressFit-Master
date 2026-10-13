using PressFit.Core.Enums;
using PressFit.Core.Interfaces;
using PressFit.Core.Models;

namespace PressFit.Communication.Alarm;

/// <summary>
/// 工业 ISA-18.2 报警生命周期管理引擎
/// 具备报警风暴抑制、状态转换机 (Active/Ack/Clear) 与历史落库审计
/// </summary>
public class AlarmManagerService : IAlarmManagerService
{
    private readonly IPressFitRepository _repository;
    private readonly List<AlarmRecord> _activeAlarms = new();
    private readonly object _lock = new();

    public IReadOnlyList<AlarmRecord> ActiveAlarms
    {
        get
        {
            lock (_lock) return _activeAlarms.ToList();
        }
    }

    public int UnacknowledgedCount
    {
        get
        {
            lock (_lock) return _activeAlarms.Count(a => !a.IsAcknowledged);
        }
    }

    public AlarmRecord? HighestPriorityActiveAlarm
    {
        get
        {
            lock (_lock) return _activeAlarms.OrderByDescending(a => a.Level).FirstOrDefault();
        }
    }

    public event Action? OnAlarmsChanged;

    public AlarmManagerService(IPressFitRepository repository)
    {
        _repository = repository;
    }

    public async Task RaiseAlarmAsync(string code, string description, AlarmLevel level)
    {
        AlarmRecord? newAlarm = null;
        lock (_lock)
        {
            // 报警风暴抑制：若当前已存在同名未恢复报警，不重复创建
            if (_activeAlarms.Any(a => a.AlarmCode == code))
            {
                return;
            }

            newAlarm = new AlarmRecord
            {
                AlarmCode = code,
                Description = description,
                Level = level,
                OccurredAt = DateTime.Now
            };
            _activeAlarms.Add(newAlarm);
        }

        if (newAlarm != null)
        {
            try
            {
                newAlarm.Id = await _repository.SaveAlarmAsync(newAlarm);
            }
            catch { }
            OnAlarmsChanged?.Invoke();
        }
    }

    public async Task AcknowledgeAlarmAsync(long alarmId)
    {
        AlarmRecord? target = null;
        lock (_lock)
        {
            target = _activeAlarms.FirstOrDefault(a => a.Id == alarmId);
            if (target != null && !target.IsAcknowledged)
            {
                target.AcknowledgedAt = DateTime.Now;
            }
        }

        if (target != null)
        {
            try
            {
                await _repository.UpdateAlarmAsync(target);
            }
            catch { }
            OnAlarmsChanged?.Invoke();
        }
    }

    public async Task AcknowledgeAllAlarmsAsync()
    {
        List<AlarmRecord> toUpdate;
        lock (_lock)
        {
            toUpdate = _activeAlarms.Where(a => !a.IsAcknowledged).ToList();
            foreach (var a in toUpdate)
            {
                a.AcknowledgedAt = DateTime.Now;
            }
        }

        foreach (var a in toUpdate)
        {
            try
            {
                await _repository.UpdateAlarmAsync(a);
            }
            catch { }
        }
        OnAlarmsChanged?.Invoke();
    }

    public async Task ClearAlarmAsync(string code)
    {
        AlarmRecord? cleared = null;
        lock (_lock)
        {
            cleared = _activeAlarms.FirstOrDefault(a => a.AlarmCode == code);
            if (cleared != null)
            {
                cleared.ClearedAt = DateTime.Now;
                _activeAlarms.Remove(cleared);
            }
        }

        if (cleared != null)
        {
            try
            {
                await _repository.UpdateAlarmAsync(cleared);
            }
            catch { }
            OnAlarmsChanged?.Invoke();
        }
    }
}
