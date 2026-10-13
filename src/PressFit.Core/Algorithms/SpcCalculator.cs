using PressFit.Core.Models;

namespace PressFit.Core.Algorithms;

/// <summary>
/// SPC 统计制程能力分析结果
/// </summary>
public record SpcResult(
    int SampleCount,
    double Mean,
    double StdDev,
    double MinValue,
    double MaxValue,
    double Cp,
    double Cpk,
    string CapabilityGrade,
    string EvaluationAdvice
);

/// <summary>
/// 工业级 SPC (Statistical Process Control) 与 CPK 制程能力分析计算引擎
/// 面向汽车零部件与新能源三电高可靠度质量统计
/// </summary>
public static class SpcCalculator
{
    /// <summary>
    /// 对样本数据（如各工件的峰值压力或终点深度）执行 CPK 统计制程能力计算
    /// </summary>
    /// <param name="dataPoints">各周期质量样本数据列表</param>
    /// <param name="usl">公差上限 (Upper Specification Limit)</param>
    /// <param name="lsl">公差下限 (Lower Specification Limit)</param>
    public static SpcResult Calculate(IReadOnlyList<double> dataPoints, double usl, double lsl)
    {
        if (dataPoints == null || dataPoints.Count < 5)
        {
            return new SpcResult(
                SampleCount: dataPoints?.Count ?? 0,
                Mean: 0,
                StdDev: 0,
                MinValue: 0,
                MaxValue: 0,
                Cp: 0,
                Cpk: 0,
                CapabilityGrade: "样本量不足",
                EvaluationAdvice: "计算 CPK 至少需要 5 组以上生产数据样本（推荐 30~50 组）。"
            );
        }

        int n = dataPoints.Count;
        double sum = 0;
        double min = double.MaxValue;
        double max = double.MinValue;

        foreach (var val in dataPoints)
        {
            sum += val;
            if (val < min) min = val;
            if (val > max) max = val;
        }

        double mean = sum / n;

        // 计算样本无偏方差与标准差 (Bessel 修正：除以 n - 1)
        double varianceSum = 0;
        foreach (var val in dataPoints)
        {
            double diff = val - mean;
            varianceSum += diff * diff;
        }

        double stdDev = Math.Sqrt(varianceSum / (n - 1));

        // 防止极小标准差导致除以零
        if (stdDev < 1e-6)
        {
            stdDev = 1e-6;
        }

        // 规格公差范围
        double tolerance = usl - lsl;
        if (tolerance <= 0)
        {
            tolerance = 1.0;
        }

        // 计算过程能力指数 Cp = (USL - LSL) / (6 * sigma)
        double cp = tolerance / (6.0 * stdDev);

        // 计算制程能力指数 Cpk = min((USL - mu) / (3 * sigma), (mu - LSL) / (3 * sigma))
        double cpu = (usl - mean) / (3.0 * stdDev);
        double cpl = (mean - lsl) / (3.0 * stdDev);
        double cpk = Math.Min(cpu, cpl);

        // 依据工业界主流标准评定等级与整改建议
        string grade;
        string advice;

        if (cpk >= 1.67)
        {
            grade = "A+ (特级)";
            advice = "制程能力过剩！质量极度稳定，不良率接近百万分之零点几(PPM)，可适当放宽管控节拍。";
        }
        else if (cpk >= 1.33)
        {
            grade = "A (优秀)";
            advice = "制程能力充分！完全符合主流车企与新能源头部电池客户验收标准，建议维持现状生产。";
        }
        else if (cpk >= 1.00)
        {
            grade = "B (警戒)";
            advice = "制程能力勉强合格，处于临界状态！存在微小波动即导致次品的风险，需排查模具或压头磨损。";
        }
        else
        {
            grade = "C (不足)";
            advice = "制程能力不足！不良率偏高，必须立即停机检查伺服电机同轴度、零件来料公差或重新标定配方。";
        }

        return new SpcResult(
            SampleCount: n,
            Mean: mean,
            StdDev: stdDev,
            MinValue: min,
            MaxValue: max,
            Cp: Math.Max(0, cp),
            Cpk: Math.Max(0, cpk),
            CapabilityGrade: grade,
            EvaluationAdvice: advice
        );
    }
}
