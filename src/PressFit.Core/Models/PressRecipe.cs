namespace PressFit.Core.Models;

/// <summary>
/// 压装产品工艺配方 (Recipe)
/// 包含工艺物理参数与精益生产计件成本核算指标
/// </summary>
public class PressRecipe
{
    public int Id { get; set; }
    public string RecipeCode { get; set; } = string.Empty; // 配方编号 (如 REC-MOTOR-001)
    public string ProductName { get; set; } = string.Empty; // 产品型号名称
    public double TargetDisplacement { get; set; } = 25.0; // 目标压入深度 (mm)
    public double DisplacementTolerance { get; set; } = 0.15; // 位移允许公差 (mm)
    public double MaxForceLimit { get; set; } = 15.0; // 极限报警压力 (kN)
    public double MinForceLimit { get; set; } = 8.0;  // 最小合格压力 (kN)
    public double ApproachSpeed { get; set; } = 50.0; // 快进速度 (mm/s)
    public double PressSpeed { get; set; } = 5.0;     // 压入速度 (mm/s)
    public double HoldingTimeSec { get; set; } = 1.0; // 保压时间 (s)
    public double EnvelopeMarginPercentage { get; set; } = 10.0; // 包络线公差窗口上下浮动比例 (%)

    // 精益生产与 OEE 产值成本核算字段
    public double UnitPrice { get; set; } = 2.50;  // 单件良品加工单价 (元)
    public double ScrapCost { get; set; } = 12.00; // 次品报废扣罚成本 (元)
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
