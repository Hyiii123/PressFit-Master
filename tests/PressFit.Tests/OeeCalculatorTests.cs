using PressFit.Core.Algorithms;
using Xunit;

namespace PressFit.Tests;

public class OeeCalculatorTests
{
    [Fact]
    public void Calculate_WhenOperatingMatchesPlannedAndAllOk_ReturnsWorldClassOee()
    {
        // 计划时间 3600s, 运行 3600s (A = 1.0)
        // 标准节拍 30s/件, 生产 120 件 (120*30 = 3600s, P = 1.0)
        // 120 件全部 OK (Q = 1.0)
        // OEE 期望 100%
        var res = OeeCalculator.Calculate(
            plannedTimeSec: 3600,
            operatingTimeSec: 3600,
            totalCount: 120,
            okCount: 120,
            idealCycleTimeSec: 30.0,
            unitPrice: 2.0,
            scrapCost: 10.0
        );

        Assert.Equal(100.0, res.OeePercentage);
        Assert.Equal(1.0, res.Availability);
        Assert.Equal(1.0, res.Performance);
        Assert.Equal(1.0, res.QualityRate);
        Assert.Equal(240.0, res.TotalOutputValue); // 120 * 2.0 = 240
        Assert.Equal(0.0, res.TotalScrapLoss);
        Assert.Contains("World Class", res.OeeEvaluationGrade);
    }

    [Fact]
    public void Calculate_WhenQualityDrops_OeeAndEconomicLossAreCalculated()
    {
        // 100 件中 80 件 OK, 20 件 NG (Q = 0.8)
        var res = OeeCalculator.Calculate(
            plannedTimeSec: 3600,
            operatingTimeSec: 3600,
            totalCount: 100,
            okCount: 80,
            idealCycleTimeSec: 36.0,
            unitPrice: 3.0,
            scrapCost: 15.0
        );

        Assert.Equal(0.8, res.QualityRate);
        Assert.Equal(240.0, res.TotalOutputValue); // 80 * 3 = 240
        Assert.Equal(300.0, res.TotalScrapLoss);   // 20 * 15 = 300
        Assert.Equal(-60.0, res.NetEconomicValue); // 净亏损 60 元
    }
}
