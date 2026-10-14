using PressFit.Communication.Modbus;
using PressFit.Core.Enums;
using PressFit.Core.Interfaces;
using PressFit.Core.Models;

namespace PressFit.Communication.Drivers;

/// <summary>
/// 工业下位机驱动工厂 (PlcDriverFactory)
/// 根据现场通信配置无缝热创建 Modbus TCP 驱动或西门子 S7 驱动
/// </summary>
public static class PlcDriverFactory
{
    public static IIndustrialPlcDriver CreateDriver(CommunicationConfig config)
    {
        if (config.Protocol == PlcProtocolType.SiemensS7)
        {
            return new SiemensS7Driver
            {
                Rack = config.SiemensRack,
                Slot = config.SiemensSlot,
                DefaultDbNumber = config.SiemensDbNumber
            };
        }

        // 默认创建高性能 Modbus TCP 客户端
        return new ModbusTcpClientService();
    }
}
