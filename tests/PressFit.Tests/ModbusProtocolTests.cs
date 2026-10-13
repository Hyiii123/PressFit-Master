using PressFit.Communication.Modbus;
using Xunit;

namespace PressFit.Tests;

public class ModbusProtocolTests
{
    [Fact]
    public void ModbusAddressMap_RegistersAndCoils_AreConfiguredCorrectly()
    {
        Assert.Equal(0, ModbusAddressMap.REG_MACHINE_STATE);
        Assert.Equal(1, ModbusAddressMap.REG_PRESS_PHASE);
        Assert.Equal(2, ModbusAddressMap.REG_DISPLACEMENT_SCALED);
        Assert.Equal(3, ModbusAddressMap.REG_FORCE_SCALED);
        Assert.Equal(4, ModbusAddressMap.REG_PEAK_FORCE_SCALED);
        Assert.Equal(5, ModbusAddressMap.REG_CYCLE_TIME_MS);

        Assert.Equal(0, ModbusAddressMap.COIL_CMD_START);
        Assert.Equal(1, ModbusAddressMap.COIL_CMD_STOP);
        Assert.Equal(4, ModbusAddressMap.COIL_STATUS_CYCLE_DONE);
        Assert.Equal(5, ModbusAddressMap.COIL_STATUS_QUALITY_OK);
    }
}
