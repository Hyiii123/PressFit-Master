using PressFit.Core.Models;

namespace PressFit.Core.Interfaces;

/// <summary>
/// 生产数据、工艺配方与报警履历仓储接口
/// </summary>
public interface IPressFitRepository
{
    Task InitializeAsync();

    // 生产履历
    Task<long> SaveProductionRecordAsync(PressResultRecord record);
    Task<IReadOnlyList<PressResultRecord>> QueryRecordsAsync(DateTime startTime, DateTime endTime, string? barcode = null, int limit = 100);

    // 配方管理
    Task<IReadOnlyList<PressRecipe>> GetAllRecipesAsync();
    Task<PressRecipe?> GetRecipeByCodeAsync(string recipeCode);
    Task<int> SaveRecipeAsync(PressRecipe recipe);
    Task DeleteRecipeAsync(int id);

    // 工业报警审计台账
    Task<long> SaveAlarmAsync(AlarmRecord alarm);
    Task UpdateAlarmAsync(AlarmRecord alarm);
    Task<IReadOnlyList<AlarmRecord>> QueryAlarmsAsync(DateTime startTime, DateTime endTime, int limit = 200);
}
