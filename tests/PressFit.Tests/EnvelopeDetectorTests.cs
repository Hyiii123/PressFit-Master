using PressFit.Core.Algorithms;
using PressFit.Core.Enums;
using PressFit.Core.Models;
using Xunit;

namespace PressFit.Tests;

public class EnvelopeDetectorTests
{
    private readonly PressRecipe _standardRecipe = new()
    {
        RecipeCode = "REC-TEST-001",
        ProductName = "测试电机定子",
        TargetDisplacement = 25.0,
        DisplacementTolerance = 0.2,
        MaxForceLimit = 16.0,
        MinForceLimit = 8.0,
        ApproachSpeed = 30.0,
        PressSpeed = 3.0,
        HoldingTimeSec = 1.0,
        EnvelopeMarginPercentage = 10.0
    };

    [Fact]
    public void Evaluate_WhenPointsEmpty_ReturnsNG_CycleTimeout()
    {
        var points = new List<PressPoint>();
        var result = EnvelopeDetector.Evaluate(points, _standardRecipe);

        Assert.False(result.IsPass);
        Assert.Equal(QualityStatus.NG_CycleTimeout, result.Status);
    }

    [Fact]
    public void Evaluate_WhenTrajectoryConforms_ReturnsOK()
    {
        var points = new List<PressPoint>();
        // 生成合规的标准平滑曲线
        for (int i = 0; i <= 50; i++)
        {
            double disp = (25.0 / 50) * i;
            double force = EnvelopeDetector.CalculateTheoreticalForce(disp, _standardRecipe);
            points.Add(new PressPoint(disp, force, i * 20));
        }

        var result = EnvelopeDetector.Evaluate(points, _standardRecipe);

        Assert.True(result.IsPass);
        Assert.Equal(QualityStatus.OK, result.Status);
        Assert.Null(result.FirstViolationPoint);
    }

    [Fact]
    public void Evaluate_WhenPeakForceExceedsMaxLimit_ReturnsNG_ForceExceeded()
    {
        var points = new List<PressPoint>();
        for (int i = 0; i <= 50; i++)
        {
            double disp = (25.0 / 50) * i;
            double force = (i == 40) ? 22.0 : EnvelopeDetector.CalculateTheoreticalForce(disp, _standardRecipe);
            points.Add(new PressPoint(disp, force, i * 20));
        }

        var result = EnvelopeDetector.Evaluate(points, _standardRecipe);

        Assert.False(result.IsPass);
        Assert.True(result.Status == QualityStatus.NG_ForceExceeded || result.Status == QualityStatus.NG_EnvelopeFault);
    }

    [Fact]
    public void Evaluate_WhenPeakForceBelowMinLimit_ReturnsNG_ForceTooLow()
    {
        var points = new List<PressPoint>();
        // 峰值仅有 3.0kN (低于 MinForceLimit 8.0kN)
        for (int i = 0; i <= 50; i++)
        {
            double disp = (25.0 / 50) * i;
            points.Add(new PressPoint(disp, 0.5 + i * 0.05, i * 20));
        }

        var result = EnvelopeDetector.Evaluate(points, _standardRecipe);

        Assert.False(result.IsPass);
        Assert.True(result.Status == QualityStatus.NG_ForceTooLow || result.Status == QualityStatus.NG_EnvelopeFault);
    }

    [Fact]
    public void Evaluate_WhenFinalDisplacementShort_ReturnsNG_Displacement()
    {
        var points = new List<PressPoint>();
        // 目标为 25.0mm，仅压入 20.0mm 提前停机
        for (int i = 0; i <= 40; i++)
        {
            double disp = (20.0 / 40) * i;
            double force = EnvelopeDetector.CalculateTheoreticalForce(disp, _standardRecipe);
            points.Add(new PressPoint(disp, force, i * 20));
        }

        var result = EnvelopeDetector.Evaluate(points, _standardRecipe);

        Assert.False(result.IsPass);
        Assert.True(result.Status == QualityStatus.NG_Displacement || result.Status == QualityStatus.NG_EnvelopeFault);
    }
}
