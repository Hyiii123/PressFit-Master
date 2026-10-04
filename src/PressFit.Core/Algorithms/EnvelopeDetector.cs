using PressFit.Core.Enums;
using PressFit.Core.Models;

namespace PressFit.Core.Algorithms;

/// <summary>
/// 包络线公差分析评估结果
/// </summary>
public record EnvelopeEvaluationResult(
    bool IsPass,
    QualityStatus Status,
    double PeakForce,
    double FinalDisplacement,
    PressPoint? FirstViolationPoint,
    string EvaluationMessage
);

/// <summary>
/// 工业级伺服压装位移-压力包络线 (Displacement-Force Envelope) 质量判定引擎
/// </summary>
public class EnvelopeDetector
{
    /// <summary>
    /// 对压装全轨迹曲线执行包络线公差带分析
    /// </summary>
    /// <param name="points">采集到的实时时序点序列</param>
    /// <param name="recipe">当前执行的产品工艺配方</param>
    public static EnvelopeEvaluationResult Evaluate(IReadOnlyList<PressPoint> points, PressRecipe recipe)
    {
        if (points == null || points.Count == 0)
        {
            return new EnvelopeEvaluationResult(
                false,
                QualityStatus.NG_CycleTimeout,
                0,
                0,
                null,
                "曲线数据为空，压装采集异常"
            );
        }

        double peakForce = 0;
        double finalDisplacement = points[^1].Displacement;

        foreach (var p in points)
        {
            if (p.Force > peakForce)
            {
                peakForce = p.Force;
            }
        }

        // 1. 终点位移绝对公差校验
        double minTargetDisp = recipe.TargetDisplacement - recipe.DisplacementTolerance;
        double maxTargetDisp = recipe.TargetDisplacement + recipe.DisplacementTolerance;

        if (finalDisplacement < minTargetDisp)
        {
            return new EnvelopeEvaluationResult(
                false,
                QualityStatus.NG_Displacement,
                peakForce,
                finalDisplacement,
                points[^1],
                $"终点位移未达标: {finalDisplacement:F2} mm (要求 >= {minTargetDisp:F2} mm)"
            );
        }

        if (finalDisplacement > maxTargetDisp)
        {
            return new EnvelopeEvaluationResult(
                false,
                QualityStatus.NG_Displacement,
                peakForce,
                finalDisplacement,
                points[^1],
                $"终点位移超程: {finalDisplacement:F2} mm (要求 <= {maxTargetDisp:F2} mm)"
            );
        }

        // 2. 最大峰值压力绝对范围校验
        if (peakForce > recipe.MaxForceLimit)
        {
            return new EnvelopeEvaluationResult(
                false,
                QualityStatus.NG_ForceExceeded,
                peakForce,
                finalDisplacement,
                points.FirstOrDefault(p => p.Force > recipe.MaxForceLimit),
                $"最大压力超限过盈卡滞: {peakForce:F2} kN (上限 {recipe.MaxForceLimit:F2} kN)"
            );
        }

        if (peakForce < recipe.MinForceLimit)
        {
            return new EnvelopeEvaluationResult(
                false,
                QualityStatus.NG_ForceTooLow,
                peakForce,
                finalDisplacement,
                points[^1],
                $"最大压力过低虚压配合: {peakForce:F2} kN (下限 {recipe.MinForceLimit:F2} kN)"
            );
        }

        // 3. 全行程位移-压力动态包络线判定 (Dynamic Envelope Curve Check)
        // 压入阶段 (> 2.0mm 接触工件后) 进行包络线公差带比对
        double marginFactor = recipe.EnvelopeMarginPercentage / 100.0;
        foreach (var p in points)
        {
            if (p.Displacement < 2.0) continue; // 快速接近段不作压力严格判定

            // 计算该位移下的理论基准期望压力 (非线性弹塑性参考曲线模型)
            double expectedForce = CalculateTheoreticalForce(p.Displacement, recipe);
            double upperLimit = expectedForce * (1.0 + marginFactor) + 0.5; // +0.5kN 基础带宽公差
            double lowerLimit = Math.Max(0.1, expectedForce * (1.0 - marginFactor) - 0.5);

            if (p.Force > upperLimit)
            {
                return new EnvelopeEvaluationResult(
                    false,
                    QualityStatus.NG_EnvelopeFault,
                    peakForce,
                    finalDisplacement,
                    p,
                    $"轨迹向上穿透公差带: 位移 {p.Displacement:F2}mm 时压力 {p.Force:F2}kN (上限 {upperLimit:F2}kN)"
                );
            }

            if (p.Force < lowerLimit && p.Displacement > (recipe.TargetDisplacement * 0.5))
            {
                return new EnvelopeEvaluationResult(
                    false,
                    QualityStatus.NG_EnvelopeFault,
                    peakForce,
                    finalDisplacement,
                    p,
                    $"轨迹向下跌出公差带: 位移 {p.Displacement:F2}mm 时压力 {p.Force:F2}kN (下限 {lowerLimit:F2}kN)"
                );
            }
        }

        return new EnvelopeEvaluationResult(
            true,
            QualityStatus.OK,
            peakForce,
            finalDisplacement,
            null,
            "压装工艺合格 (OK)"
        );
    }

    /// <summary>
    /// 根据物理模型计算理论基准位移-压力参考值
    /// </summary>
    public static double CalculateTheoreticalForce(double displacement, PressRecipe recipe)
    {
        if (displacement <= 2.0) return 0.2; // 接触前仅有自重摩擦力

        // 压入进程百分比 (0.0 ~ 1.0)
        double progress = Math.Clamp((displacement - 2.0) / Math.Max(1.0, recipe.TargetDisplacement - 2.0), 0.0, 1.0);

        // 采用典型过盈配合 S 型渐进抗力曲线: F = Min + (TargetMax - Min) * (progress ^ 1.3)
        double targetMidForce = (recipe.MaxForceLimit + recipe.MinForceLimit) * 0.5;
        return 0.5 + (targetMidForce - 0.5) * Math.Pow(progress, 1.25);
    }
}
