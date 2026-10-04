using Dapper;
using Microsoft.Data.Sqlite;

namespace PressFit.Data;

/// <summary>
/// SQLite 数据库初始化器
/// 负责建表、初始化默认工艺配方种子数据、开启 WAL 高性能模式
/// </summary>
public static class DatabaseInitializer
{
    public const string ConnectionString = ""Data Source=pressfit_production.db"";

    public static async Task InitializeAsync()
    {
        using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();

        // 开启 WAL (Write-Ahead Logging) 模式与同步优化，实现高并发无锁读写
        await connection.ExecuteAsync(""PRAGMA journal_mode = WAL;"");
        await connection.ExecuteAsync(""PRAGMA synchronous = NORMAL;"");

        // 1. 创建配方表
        string createRecipeSql = @""
            CREATE TABLE IF NOT EXISTS recipes (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                RecipeCode TEXT UNIQUE NOT NULL,
                ProductName TEXT NOT NULL,
                TargetDisplacement REAL NOT NULL,
                DisplacementTolerance REAL NOT NULL,
                MaxForceLimit REAL NOT NULL,
                MinForceLimit REAL NOT NULL,
                ApproachSpeed REAL NOT NULL,
                PressSpeed REAL NOT NULL,
                HoldingTimeSec REAL NOT NULL,
                EnvelopeMarginPercentage REAL NOT NULL,
                UpdatedAt TEXT NOT NULL
            );
        "";
        await connection.ExecuteAsync(createRecipeSql);

        // 2. 创建生产履历表
        string createRecordSql = @""
            CREATE TABLE IF NOT EXISTS production_records (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                WorkpieceBarcode TEXT NOT NULL,
                RecipeCode TEXT NOT NULL,
                Quality INTEGER NOT NULL,
                PeakForce REAL NOT NULL,
                FinalDisplacement REAL NOT NULL,
                CycleDurationSec REAL NOT NULL,
                FailureReason TEXT,
                CurvePointsJson TEXT,
                CreatedAt TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_records_time ON production_records(CreatedAt);
            CREATE INDEX IF NOT EXISTS idx_records_barcode ON production_records(WorkpieceBarcode);
        "";
        await connection.ExecuteAsync(createRecordSql);

        // 3. 注入初始默认演示配方 (若为空)
        int recipeCount = await connection.ExecuteScalarAsync<int>(""SELECT COUNT(1) FROM recipes;"");
        if (recipeCount == 0)
        {
            string seedSql = @""
                INSERT INTO recipes (RecipeCode, ProductName, TargetDisplacement, DisplacementTolerance, MaxForceLimit, MinForceLimit, ApproachSpeed, PressSpeed, HoldingTimeSec, EnvelopeMarginPercentage, UpdatedAt)
                VALUES 
                ('REC-MOTOR-101', '新能源驱动电机定子铁芯压装', 28.50, 0.10, 18.00, 10.00, 45.0, 4.0, 1.2, 10.0, datetime('now', 'localtime')),
                ('REC-BEAR-202', '精密主轴高速轴承过盈装配', 15.20, 0.08, 12.50, 7.50, 30.0, 3.0, 1.0, 8.0, datetime('now', 'localtime')),
                ('REC-PIN-303', '行星减速器定位销自动压接', 8.00, 0.05, 6.00, 3.50, 20.0, 2.0, 0.8, 12.0, datetime('now', 'localtime'));
            "";
            await connection.ExecuteAsync(seedSql);
        }
    }
}
