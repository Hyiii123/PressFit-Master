using PressFit.Core.Models;

namespace PressFit.Core.Interfaces;

/// <summary>
/// 生产数据与配方仓储接口
/// </summary>
public interface IPressFitRepository
{
    Task InitializeAsync();
    Task<long> SaveProductionRecordAsync(PressResultRecord record);
    Task<IReadOnlyList<PressResultRecord>> QueryRecordsAsync(DateTime startTime, DateTime endTime, string? barcode = null, int limit = 100);
    Task<IReadOnlyList<PressRecipe>> GetAllRecipesAsync();
    Task<PressRecipe?> GetRecipeByCodeAsync(string recipeCode);
    Task<int> SaveRecipeAsync(PressRecipe recipe);
    Task DeleteRecipeAsync(int id);
}
