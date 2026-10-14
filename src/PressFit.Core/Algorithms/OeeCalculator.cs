namespace PressFit.Core.Algorithms;

/// <summary>
/// OEE 综合设备效率与精益制造产值统计结果
/// </summary>
public record OeeCalculationResult(
    double Availability,       // 时间稼动率 / 可用率 (0.0 ~ 1.0)
    double Performance,        // 性能稼动率 / 表现指数 (0.0 ~ 1.0)
    double QualityRate,        // 质量指数 / 良品率 (0.0 ~ 1.0)
    double OeePercentage,      // 综合设备效率 OEE (0.0% ~ 100.0%)
    double TotalOutputValue,   // 累计产值 (元)
    double TotalScrapLoss,     // 报废损失成本 (元)
    double NetEconomicValue,   // 净生产价值 (元)
    string OeeEvaluationGrade  // 评级建议 (世界级/良好/需改善)
);

/// <summary>
/// 工业 OEE (Overall Equipment Effectiveness) 黄金三角计算引擎
/// </summary>
public static class OeeCalculator
{
    public static OeeCalculationResult Calculate(
        double plannedTimeSec,
        double operatingTimeSec,
        int totalCount,
        int okCount,
        double idealCycleTimeSec,
        double unitPrice = 2.50,
        double scrapCost = 12.00)
    {
        int ngCount = Math.Max(0, totalCount - okCount);

        // 1. 时间稼动率 (Availability) = 实际运行时间 / 计划开机时间
        double availability = plannedTimeSec > 0 ? Math.Clamp(operatingTimeSec / plannedTimeSec, 0.0, 1.0) : 1.0;

        // 2. 性能稼动率 (Performance) = (理论标准节拍 * 总生产件数) / 实际运行时间
        double performance = operatingTimeSec > 0 ? Math.Clamp((idealCycleTimeSec * totalCount) / operatingTimeSec, 0.0, 1.2) : 1.0;

        // 3. 质量指数 (Quality) = 良品数量 / 总生产件数
        double quality = totalCount > 0 ? Math.Clamp((double)okCount / totalCount, 0.0, 1.0) : 1.0;

        // 4. 综合 OEE = A * P * Q
        double oee = availability * performance * quality * 100.0;

        // 经济指标计算
        double outputValue = okCount * unitPrice;
        double scrapLoss = ngCount * scrapCost;
        double netValue = outputValue - scrapLoss;

        // 依据精益制造工业标准评定
        string grade;
        if (oee >= 85.0)
            grade = "世界级卓越产线 (World Class OEE >= 85%)";
        else if (oee >= 70.0)
            grade = "优良稼动产线 (Good OEE 70%~85%)";
        else if (oee >= 55.0)
            grade = "中等水平产线 (Moderate OEE 55%~70%)";
        else
            grade = "低稼动/需停机整改 (OEE < 55%)";

        return new OeeCalculationResult(
            Availability: availability,
            Performance: performance,
            QualityRate: quality,
            OeePercentage: Math.Round(oee, 2),
            TotalOutputValue: Math.Round(outputValue, 2),
            TotalScrapLoss: Math.Round(scrapLoss, 2),
            NetEconomicValue: Math.Round(netValue, 2),
            OeeEvaluationGrade: grade
        );
    }
}
