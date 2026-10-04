using PressFit.Core.Models;
using PressFit.Simulator.Faults;

namespace PressFit.Simulator.Physics;

/// <summary>
/// 伺服压装动力学物理特征发生器
/// 生成具备物理惯性、弹塑性抗力与高斯测量白噪声的高保真时序数据
/// </summary>
public class PressCurveGenerator
{
    private readonly Random _random = new();

    /// <summary>
    /// 根据配方与故障配置生成全行程压装曲线点序列
    /// </summary>
    public List<PressPoint> GenerateTrajectory(PressRecipe recipe, FaultInjectionConfig faults)
    {
        var points = new List<PressPoint>();
        double targetDisp = recipe.TargetDisplacement;

        if (faults.SimulateDisplacementShortNG)
        {
            targetDisp *= 0.85; // 未到位提前卡死
        }

        double currentDisp = 0.0;
        double currentForce = 0.0;
        double currentTimeMs = 0.0;
        double timeStepMs = 20.0; // 50Hz 采样时步

        // 阶段 1: 快进接近工件 (0 ~ 2.0mm)
        while (currentDisp < 2.0)
        {
            currentDisp += recipe.ApproachSpeed * (timeStepMs / 1000.0);
            currentForce = 0.15 + (_random.NextDouble() * 0.05); // 仅摩擦底噪
            points.Add(new PressPoint(currentDisp, currentForce, currentTimeMs));
            currentTimeMs += timeStepMs;
        }

        // 阶段 2: 恒速受阻可控压入 (2.0mm ~ targetDisp)
        while (currentDisp < targetDisp)
        {
            currentDisp += recipe.PressSpeed * (timeStepMs / 1000.0);
            if (currentDisp > targetDisp) currentDisp = targetDisp;

            double progress = (currentDisp - 2.0) / Math.Max(1.0, recipe.TargetDisplacement - 2.0);
            double baseForce = (recipe.MaxForceLimit + recipe.MinForceLimit) * 0.5;

            // 非线性弹塑性抗力曲线
            currentForce = 0.3 + (baseForce - 0.3) * Math.Pow(progress, 1.25);

            // 叠加高斯测量噪声 (+-0.08 kN)
            currentForce += (_random.NextDouble() - 0.5) * 0.16;

            // 故障注入：异常超限压力
            if (faults.SimulateForceSpikeNG && progress > 0.6)
            {
                currentForce = recipe.MaxForceLimit * 1.35; // 瞬间飙升 135%
            }

            // 故障注入：异常偏低压力
            if (faults.SimulateForceLowNG)
            {
                currentForce *= 0.45; // 仅有 45% 压力
            }

            points.Add(new PressPoint(currentDisp, Math.Max(0.1, currentForce), currentTimeMs));
            currentTimeMs += timeStepMs;
        }

        // 阶段 3: 保压阶段 (Holding)
        double holdEndTime = currentTimeMs + (recipe.HoldingTimeSec * 1000.0);
        while (currentTimeMs < holdEndTime)
        {
            double holdNoise = (_random.NextDouble() - 0.5) * 0.05;
            points.Add(new PressPoint(targetDisp, currentForce + holdNoise, currentTimeMs));
            currentTimeMs += timeStepMs;
        }

        return points;
    }
}
