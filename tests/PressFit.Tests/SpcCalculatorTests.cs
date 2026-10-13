using PressFit.Core.Algorithms;
using Xunit;

namespace PressFit.Tests;

public class SpcCalculatorTests
{
    [Fact]
    public void Calculate_WhenSamplesLessThan5_ReturnsInsufficientSamples()
    {
        var data = new List<double> { 12.0, 12.2, 12.1 };
        var res = SpcCalculator.Calculate(data, usl: 15.0, lsl: 9.0);

        Assert.Equal("样本量不足", res.CapabilityGrade);
        Assert.Equal(3, res.SampleCount);
    }

    [Fact]
    public void Calculate_WhenCenteringIdealAndVarianceLow_ReturnsGradeAPlus()
    {
        // 规格: 10.0 ~ 14.0，中心为 12.0
        // 样本均紧密聚集在 12.0 附近
        var data = new List<double> { 12.01, 11.99, 12.02, 11.98, 12.00, 12.01, 11.99, 12.00 };
        var res = SpcCalculator.Calculate(data, usl: 14.0, lsl: 10.0);

        Assert.True(res.Cpk >= 1.67);
        Assert.Contains("A+", res.CapabilityGrade);
    }

    [Fact]
    public void Calculate_WhenMeanShiftedTowardUSL_CpkIsLowerThanCp()
    {
        // 规格: 10.0 ~ 14.0
        // 均值明显偏向 13.8 (偏离中心，接近上限)
        var data = new List<double> { 13.75, 13.80, 13.82, 13.78, 13.85, 13.79 };
        var res = SpcCalculator.Calculate(data, usl: 14.0, lsl: 10.0);

        // 偏离中心时，Cpk 必定明显小于 Cp
        Assert.True(res.Cpk < res.Cp);
    }
}
