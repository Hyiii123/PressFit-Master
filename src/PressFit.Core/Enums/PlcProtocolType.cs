namespace PressFit.Core.Enums;

/// <summary>
/// 工业下位机通信协议类型
/// </summary>
public enum PlcProtocolType
{
    ModbusTcp = 0, // 标准 Modbus TCP 协议 (汇川/台达/工控仪表)
    SiemensS7 = 1  // 西门子 S7 专有以太网协议 (S7-1200 / S7-1500 / S7-300)
}
