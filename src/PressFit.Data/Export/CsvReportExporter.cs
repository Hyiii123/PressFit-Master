using System.Text;
using PressFit.Core.Models;

namespace PressFit.Data.Export;

/// <summary>
/// 工业生产数据与质量台账 CSV 报表导出器
/// </summary>
public static class CsvReportExporter
{
    public static async Task ExportToCsvAsync(string filePath, IEnumerable<PressResultRecord> records)
    {
        var sb = new StringBuilder();
        // 写入标准 CSV 表头
        sb.AppendLine("记录ID,工件条码(SN),配方编号,品质判定,峰值压力(kN),最终位移(mm),节拍耗时(s),异常原因,生产时间戳");

        foreach (var r in records)
        {
            string line = $"{r.Id},\"{r.WorkpieceBarcode}\",\"{r.RecipeCode}\",\"{r.Quality}\",{r.PeakForce:F2},{r.FinalDisplacement:F2},{r.CycleDurationSec:F2},\"{r.FailureReason?.Replace("\"", "\"\"")}\",\"{r.CreatedAt:yyyy-MM-dd HH:mm:ss}\"";
            sb.AppendLine(line);
        }

        // 使用带 UTF8 BOM 编码保存，避免 Windows Excel 打开时中文乱码
        await File.WriteAllTextAsync(filePath, sb.ToString(), new UTF8Encoding(true));
    }
}
