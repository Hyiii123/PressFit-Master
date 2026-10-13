using PressFit.Communication.Alarm;
using PressFit.Core.Enums;
using PressFit.Core.Interfaces;
using PressFit.Core.Models;
using Xunit;

namespace PressFit.Tests;

public class MockRepository : IPressFitRepository
{
    public List<AlarmRecord> SavedAlarms { get; } = new();

    public Task InitializeAsync() => Task.CompletedTask;
    public Task<long> SaveProductionRecordAsync(PressResultRecord record) => Task.FromResult(1L);
    public Task<IReadOnlyList<PressResultRecord>> QueryRecordsAsync(DateTime startTime, DateTime endTime, string? barcode = null, int limit = 100) => Task.FromResult<IReadOnlyList<PressResultRecord>>(new List<PressResultRecord>());
    public Task<IReadOnlyList<PressRecipe>> GetAllRecipesAsync() => Task.FromResult<IReadOnlyList<PressRecipe>>(new List<PressRecipe>());
    public Task<PressRecipe?> GetRecipeByCodeAsync(string recipeCode) => Task.FromResult<PressRecipe?>(null);
    public Task<int> SaveRecipeAsync(PressRecipe recipe) => Task.FromResult(1);
    public Task DeleteRecipeAsync(int id) => Task.CompletedTask;

    public Task<long> SaveAlarmAsync(AlarmRecord alarm)
    {
        alarm.Id = SavedAlarms.Count + 1;
        SavedAlarms.Add(alarm);
        return Task.FromResult(alarm.Id);
    }

    public Task UpdateAlarmAsync(AlarmRecord alarm) => Task.CompletedTask;
    public Task<IReadOnlyList<AlarmRecord>> QueryAlarmsAsync(DateTime startTime, DateTime endTime, int limit = 200) => Task.FromResult<IReadOnlyList<AlarmRecord>>(SavedAlarms);
}

public class AlarmManagerTests
{
    [Fact]
    public async Task RaiseAlarmAsync_WhenNew_AddsToActiveList()
    {
        var repo = new MockRepository();
        var manager = new AlarmManagerService(repo);

        await manager.RaiseAlarmAsync("ALM-E-STOP", "急停按钮被拍下", AlarmLevel.Fatal);

        Assert.Single(manager.ActiveAlarms);
        Assert.Equal(1, manager.UnacknowledgedCount);
        Assert.Equal("ALM-E-STOP", manager.HighestPriorityActiveAlarm?.AlarmCode);
    }

    [Fact]
    public async Task RaiseAlarmAsync_WhenSameCodeAlreadyActive_SuppressesDuplicate()
    {
        var repo = new MockRepository();
        var manager = new AlarmManagerService(repo);

        // 模拟连续触发 3 次同一报警 (报警风暴)
        await manager.RaiseAlarmAsync("ALM-SERVO", "伺服过载", AlarmLevel.Error);
        await manager.RaiseAlarmAsync("ALM-SERVO", "伺服过载", AlarmLevel.Error);
        await manager.RaiseAlarmAsync("ALM-SERVO", "伺服过载", AlarmLevel.Error);

        // 抑制机制生效，仅保留一条活动记录
        Assert.Single(manager.ActiveAlarms);
    }

    [Fact]
    public async Task AcknowledgeAlarmAsync_SetsAcknowledgedTime()
    {
        var repo = new MockRepository();
        var manager = new AlarmManagerService(repo);

        await manager.RaiseAlarmAsync("ALM-FORCE", "压力超限", AlarmLevel.Warning);
        long id = manager.ActiveAlarms[0].Id;

        await manager.AcknowledgeAlarmAsync(id);

        Assert.Equal(0, manager.UnacknowledgedCount);
        Assert.True(manager.ActiveAlarms[0].IsAcknowledged);
    }

    [Fact]
    public async Task ClearAlarmAsync_RemovesFromActiveAlarms()
    {
        var repo = new MockRepository();
        var manager = new AlarmManagerService(repo);

        await manager.RaiseAlarmAsync("ALM-TEMP", "电机温度偏高", AlarmLevel.Warning);
        Assert.Single(manager.ActiveAlarms);

        await manager.ClearAlarmAsync("ALM-TEMP");

        Assert.Empty(manager.ActiveAlarms);
    }
}
