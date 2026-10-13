using Dapper;
using Microsoft.Data.Sqlite;
using PressFit.Core.Interfaces;
using PressFit.Core.Models;

namespace PressFit.Data.Repositories;

/// <summary>
/// 基于 Dapper 的生产数据、工艺配方与报警履历高效仓储实现
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
        string sql = @"
            INSERT INTO production_records (
                WorkpieceBarcode, RecipeCode, Quality, PeakForce, FinalDisplacement, 
                CycleDurationSec, FailureReason, CurvePointsJson, CreatedAt
            ) VALUES (
                @WorkpieceBarcode, @RecipeCode, @Quality, @PeakForce, @FinalDisplacement, 
                @CycleDurationSec, @FailureReason, @CurvePointsJson, @CreatedAt
            );
            SELECT last_insert_rowid();
        ";
        return await conn.ExecuteScalarAsync<long>(sql, record);
    }

    public async Task<IReadOnlyList<PressResultRecord>> QueryRecordsAsync(DateTime startTime, DateTime endTime, string? barcode = null, int limit = 100)
    {
        using var conn = new SqliteConnection(DatabaseInitializer.ConnectionString);
        string sql = @"
            SELECT * FROM production_records 
            WHERE CreatedAt >= @StartTime AND CreatedAt <= @EndTime
        ";

        if (!string.IsNullOrWhiteSpace(barcode))
        {
            sql += " AND WorkpieceBarcode LIKE '%' || @Barcode || '%'";
        }

        sql += " ORDER BY Id DESC LIMIT @Limit;";

        var result = await conn.QueryAsync<PressResultRecord>(sql, new { StartTime = startTime.ToString("yyyy-MM-dd HH:mm:ss"), EndTime = endTime.ToString("yyyy-MM-dd HH:mm:ss"), Barcode = barcode, Limit = limit });
        return result.ToList();
    }

    public async Task<IReadOnlyList<PressRecipe>> GetAllRecipesAsync()
    {
        using var conn = new SqliteConnection(DatabaseInitializer.ConnectionString);
        string sql = "SELECT * FROM recipes ORDER BY Id ASC;";
        var result = await conn.QueryAsync<PressRecipe>(sql);
        return result.ToList();
    }

    public async Task<PressRecipe?> GetRecipeByCodeAsync(string recipeCode)
    {
        using var conn = new SqliteConnection(DatabaseInitializer.ConnectionString);
        string sql = "SELECT * FROM recipes WHERE RecipeCode = @RecipeCode LIMIT 1;";
        return await conn.QueryFirstOrDefaultAsync<PressRecipe>(sql, new { RecipeCode = recipeCode });
    }

    public async Task<int> SaveRecipeAsync(PressRecipe recipe)
    {
        using var conn = new SqliteConnection(DatabaseInitializer.ConnectionString);
        recipe.UpdatedAt = DateTime.Now;

        if (recipe.Id == 0)
        {
            string insertSql = @"
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
            ";
            return await conn.ExecuteScalarAsync<int>(insertSql, recipe);
        }
        else
        {
            string updateSql = @"
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
            ";
            await conn.ExecuteAsync(updateSql, recipe);
            return recipe.Id;
        }
    }

    public async Task DeleteRecipeAsync(int id)
    {
        using var conn = new SqliteConnection(DatabaseInitializer.ConnectionString);
        await conn.ExecuteAsync("DELETE FROM recipes WHERE Id = @Id;", new { Id = id });
    }

    public async Task<long> SaveAlarmAsync(AlarmRecord alarm)
    {
        using var conn = new SqliteConnection(DatabaseInitializer.ConnectionString);
        string sql = @"
            INSERT INTO alarms (AlarmCode, Description, Level, OccurredAt, AcknowledgedAt, ClearedAt)
            VALUES (@AlarmCode, @Description, @Level, @OccurredAt, @AcknowledgedAt, @ClearedAt);
            SELECT last_insert_rowid();
        ";
        return await conn.ExecuteScalarAsync<long>(sql, new
        {
            alarm.AlarmCode,
            alarm.Description,
            Level = (int)alarm.Level,
            OccurredAt = alarm.OccurredAt.ToString("yyyy-MM-dd HH:mm:ss"),
            AcknowledgedAt = alarm.AcknowledgedAt?.ToString("yyyy-MM-dd HH:mm:ss"),
            ClearedAt = alarm.ClearedAt?.ToString("yyyy-MM-dd HH:mm:ss")
        });
    }

    public async Task UpdateAlarmAsync(AlarmRecord alarm)
    {
        using var conn = new SqliteConnection(DatabaseInitializer.ConnectionString);
        string sql = @"
            UPDATE alarms SET 
                AcknowledgedAt = @AcknowledgedAt,
                ClearedAt = @ClearedAt
            WHERE Id = @Id;
        ";
        await conn.ExecuteAsync(sql, new
        {
            alarm.Id,
            AcknowledgedAt = alarm.AcknowledgedAt?.ToString("yyyy-MM-dd HH:mm:ss"),
            ClearedAt = alarm.ClearedAt?.ToString("yyyy-MM-dd HH:mm:ss")
        });
    }

    public async Task<IReadOnlyList<AlarmRecord>> QueryAlarmsAsync(DateTime startTime, DateTime endTime, int limit = 200)
    {
        using var conn = new SqliteConnection(DatabaseInitializer.ConnectionString);
        string sql = @"
            SELECT * FROM alarms 
            WHERE OccurredAt >= @StartTime AND OccurredAt <= @EndTime
            ORDER BY Id DESC LIMIT @Limit;
        ";
        var rows = await conn.QueryAsync(sql, new
        {
            StartTime = startTime.ToString("yyyy-MM-dd HH:mm:ss"),
            EndTime = endTime.ToString("yyyy-MM-dd HH:mm:ss"),
            Limit = limit
        });

        var list = new List<AlarmRecord>();
        foreach (var r in rows)
        {
            list.Add(new AlarmRecord
            {
                Id = (long)r.Id,
                AlarmCode = (string)r.AlarmCode,
                Description = (string)r.Description,
                Level = (Core.Enums.AlarmLevel)(int)r.Level,
                OccurredAt = DateTime.Parse((string)r.OccurredAt),
                AcknowledgedAt = r.AcknowledgedAt != null ? DateTime.Parse((string)r.AcknowledgedAt) : null,
                ClearedAt = r.ClearedAt != null ? DateTime.Parse((string)r.ClearedAt) : null
            });
        }
        return list;
    }
}
