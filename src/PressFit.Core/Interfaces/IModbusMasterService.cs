using PressFit.Core.Models;

namespace PressFit.Core.Interfaces;

/// <summary>
/// Modbus TCP 通信主站驱动接口
/// </summary>
public interface IModbusMasterService
{
    bool IsConnected { get; }
    Task<bool> ConnectAsync(string ip, int port, CancellationToken ct = default);
    Task DisconnectAsync();
    Task<short[]> ReadHoldingRegistersAsync(byte unitId, ushort startAddress, ushort count, CancellationToken ct = default);
    Task WriteHoldingRegisterAsync(byte unitId, ushort address, short value, CancellationToken ct = default);
    Task WriteMultipleRegistersAsync(byte unitId, ushort startAddress, short[] values, CancellationToken ct = default);
    Task WriteSingleCoilAsync(byte unitId, ushort address, bool value, CancellationToken ct = default);
    Task<bool[]> ReadCoilsAsync(byte unitId, ushort startAddress, ushort count, CancellationToken ct = default);
}
