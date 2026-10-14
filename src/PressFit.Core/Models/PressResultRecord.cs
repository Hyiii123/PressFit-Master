using PressFit.Core.Enums;

namespace PressFit.Core.Models;

/// <summary>
/// 压装单件生产履历归档记录
/// 支持本地 SQLite 离线缓冲与断网断点补传 (Store-and-Forward)
/// </summary>
public class PressResultRecord
{
    public long Id { get; set; }
    public string WorkpieceBarcode { get; set; } = string.Empty; // 工件二维码/SN
    public string RecipeCode { get; set; } = string.Empty;       // 使用的配方编号
    public QualityStatus Quality { get; set; } = QualityStatus.Unknown; // 最终判定
    public double PeakForce { get; set; }             // 最大峰值压力 (kN)
    public double FinalDisplacement { get; set; }      // 最终终止位移 (mm)
    public double CycleDurationSec { get; set; }       // 压装总节拍时间 (s)
    public string FailureReason { get; set; } = string.Empty; // NG 详细原因
    public string CurvePointsJson { get; set; } = string.Empty; // 采样曲线数据点压缩 JSON
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // 边缘断点续传同步标志
    public bool IsSyncedToMes { get; set; } = false;   // 是否已同步至云端/MES
    public DateTime? SyncedAt { get; set; }            // 同步上报时间戳
}
