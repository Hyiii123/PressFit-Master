using PressFit.Communication.Drivers;
using PressFit.Core.Enums;
using PressFit.Core.Models;
using Xunit;

namespace PressFit.Tests;

public class PlcDriverFactoryTests
{
    [Fact]
    public void CreateDriver_WhenProtocolIsModbus_ReturnsModbusDriver()
    {
        var config = new CommunicationConfig
        {
            Protocol = PlcProtocolType.ModbusTcp,
            IpAddress = "127.0.0.1",
            Port = 5020
        };

        var driver = PlcDriverFactory.CreateDriver(config);

        Assert.Equal(PlcProtocolType.ModbusTcp, driver.ProtocolType);
    }

    [Fact]
    public void CreateDriver_WhenProtocolIsSiemensS7_ReturnsSiemensDriver()
    {
        var config = new CommunicationConfig
        {
            Protocol = PlcProtocolType.SiemensS7,
            IpAddress = "192.168.1.10",
            Port = 102,
            SiemensRack = 0,
            SiemensSlot = 1,
            SiemensDbNumber = 2
        };

        var driver = PlcDriverFactory.CreateDriver(config);

        Assert.Equal(PlcProtocolType.SiemensS7, driver.ProtocolType);
        Assert.IsType<SiemensS7Driver>(driver);
    }
}
