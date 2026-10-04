namespace PressFit.Communication.Modbus;

/// <summary>
/// 工业现场 PLC / 从机寄存器与线圈标准地址映射表
/// </summary>
public static class ModbusAddressMap
{
    // ================= 保持寄存器 (Holding Registers, 4x, 读写) =================
    public const ushort REG_MACHINE_STATE = 0;      // 设备整机状态 (0:离线, 2:待机, 3:运行, 5:报警, 6:急停)
    public const ushort REG_PRESS_PHASE = 1;        // 当前工艺阶段 (0..6)
    public const ushort REG_DISPLACEMENT_SCALED = 2;// 当前位移 (数值*100, 比如 2500 表示 25.00mm)
    public const ushort REG_FORCE_SCALED = 3;       // 当前压力 (数值*100, 比如 1250 表示 12.50kN)
    public const ushort REG_PEAK_FORCE_SCALED = 4;  // 本周期峰值压力 (数值*100)
    public const ushort REG_CYCLE_TIME_MS = 5;      // 本周期已运行时间 (ms)
    public const ushort REG_ALARM_CODE = 6;         // 当前报警代码 (0表示无告警)
    public const ushort REG_RECIPE_TARGET_DISP = 10;// 配方目标位移 (*100)
    public const ushort REG_RECIPE_MAX_FORCE = 11;  // 配方最大容许压力 (*100)
    public const ushort REG_RECIPE_MIN_FORCE = 12;  // 配方最小合格压力 (*100)

    // ================= 线圈 (Coils, 0x, 读写布尔量) =================
    public const ushort COIL_CMD_START = 0;         // 启动压装周期指令
    public const ushort COIL_CMD_STOP = 1;          // 停止/复位指令
    public const ushort COIL_CMD_RESET_ALARM = 2;   // 复位报警指令
    public const ushort COIL_STATUS_EMERGENCY = 3;  // 急停按钮触发状态
    public const ushort COIL_STATUS_CYCLE_DONE = 4; // 压装周期完成标志
    public const ushort COIL_STATUS_QUALITY_OK = 5; // 下位机预判定 OK 标志 (1:OK, 0:NG)
}
