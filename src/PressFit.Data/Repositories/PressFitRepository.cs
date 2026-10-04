using Dapper;
using Microsoft.Data.Sqlite;
using PressFit.Core.Interfaces;
using PressFit.Core.Models;

namespace PressFit.Data.Repositories;

/// <summary>
/// 基于 Dapper 的生产数据与工艺配方高效仓储实现
/// </summary>
public class PressFitRepository : IPressFitRepository
{
    public async Task InitializeAsync()
    {
        await DatabaseInitializer.InitializeAsync();
    }

    public async Task<long> SaveProductionRecordAsync(PressResultRecord record)
    {
        using var conn = new SqliteConnection(DatabaseInitializer.ConnectionString);
        string sql = @""
            INSERT INTO production_records (
                WorkpieceBarcode, RecipeCode, Quality, PeakForce, FinalDisplacement, 
                CycleDurationSec, FailureReason, CurvePointsJson, CreatedAt
            ) VALUES (
                @WorkpieceBarcode, @RecipeCode, @Quality, @PeakForce, @FinalDisplacement, 
                @CycleDurationSec, @FailureReason, @CurvePointsJson, @CreatedAt
            );
            SELECT last_insert_rowid();
        "";
        return await conn.ExecuteScalarAsync<long>(sql, record);
    }

    public async Task<IReadOnlyList<PressResultRecord>> QueryRecordsAsync(DateTime startTime, DateTime endTime, string? barcode = null, int limit = 100)
    {
        using var conn = new SqliteConnection(DatabaseInitializer.ConnectionString);
        string sql = @""
            SELECT * FROM production_records 
            WHERE CreatedAt >= @StartTime AND CreatedAt <= @EndTime
        "";

        if (!string.IsNullOrWhiteSpace(barcode))
        {
            sql += "" AND WorkpieceBarcode LIKE '%' || @Barcode || '%'"";
        }

        sql += "" ORDER BY Id DESC LIMIT @Limit;"";

        var result = await conn.QueryAsync<PressResultRecord>(sql, new { StartTime = startTime.ToString(""yyyy-MM-dd HH:mm:ss""), EndTime = endTime.ToString(""yyyy-MM-dd HH:mm:ss""), Barcode = barcode, Limit = limit });
        return result.ToList();
    }

    public async Task<IReadOnlyList<PressRecipe>> GetAllRecipesAsync()
    {
        using var conn = new SqliteConnection(DatabaseInitializer.ConnectionString);
        string sql = ""SELECT * FROM recipes ORDER BY Id ASC;"";
        var result = await conn.QueryAsync<PressRecipe>(sql);
        return result.ToList();
    }

    public async Task<PressRecipe?> GetRecipeByCodeAsync(string recipeCode)
    {
        using var conn = new SqliteConnection(DatabaseInitializer.ConnectionString);
        string sql = ""SELECT * FROM recipes WHERE RecipeCode = @RecipeCode LIMIT 1;"";
        return await conn.QueryFirstOrDefaultAsync<PressRecipe>(sql, new { RecipeCode = recipeCode });
    }

    public async Task<int> SaveRecipeAsync(PressRecipe recipe)
    {
        using var conn = new SqliteConnection(DatabaseInitializer.ConnectionString);
        recipe.UpdatedAt = DateTime.Now;

        if (recipe.Id == 0)
        {
            string insertSql = @""
                INSERT INTO recipes (
                    RecipeCode, ProductName, TargetDisplacement, DisplacementTolerance, 
                    MaxForceLimit, MinForceLimit, ApproachSpeed, PressSpeed, 
                    HoldingTimeSec, EnvelopeMarginPercentage, UpdatedAt
                ) VALUES (
                    @RecipeCode, @ProductName, @TargetDisplacement, @DisplacementTolerance, 
                    @MaxForceLimit, @MinForceLimit, @ApproachSpeed, @PressSpeed, 
                    @HoldingTimeSec, @EnvelopeMarginPercentage, @UpdatedAt
                );
                SELECT last_insert_rowid();
            "";
            return await conn.ExecuteScalarAsync<int>(insertSql, recipe);
        }
        else
        {
            string updateSql = @""
                UPDATE recipes SET 
                    RecipeCode = @RecipeCode,
                    ProductName = @ProductName,
                    TargetDisplacement = @TargetDisplacement,
                    DisplacementTolerance = @DisplacementTolerance,
                    MaxForceLimit = @MaxForceLimit,
                    MinForceLimit = @MinForceLimit,
                    ApproachSpeed = @ApproachSpeed,
                    PressSpeed = @PressSpeed,
                    HoldingTimeSec = @HoldingTimeSec,
                    EnvelopeMarginPercentage = @EnvelopeMarginPercentage,
                    UpdatedAt = @UpdatedAt
                WHERE Id = @Id;
            "";
            await conn.ExecuteAsync(updateSql, recipe);
            return recipe.Id;
        }
    }

    public async Task DeleteRecipeAsync(int id)
    {
        using var conn = new SqliteConnection(DatabaseInitializer.ConnectionString);
        await conn.ExecuteAsync(""DELETE FROM recipes WHERE Id = @Id;"", new { Id = id });
    }
}
