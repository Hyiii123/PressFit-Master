using System.Net.Sockets;
using PressFit.Core.Interfaces;
using PressFit.Core.Models;

namespace PressFit.Communication.Modbus;

/// <summary>
/// 纯原生异步高性能 Modbus TCP 主站通信客户端
/// 具备完整的 MBAP 报文封装、双向流处理、超时控制与原始帧抓包监视
/// </summary>
public class ModbusTcpClientService : IIndustrialPlcDriver, IDisposable
{
    public PressFit.Core.Enums.PlcProtocolType ProtocolType => PressFit.Core.Enums.PlcProtocolType.ModbusTcp;

    public Task<short[]> ReadDbWordsAsync(int dbNumber, int startOffset, int count, CancellationToken ct = default) => ReadHoldingRegistersAsync((byte)1, (ushort)(startOffset / 2), (ushort)count, ct);

    public Task WriteDbWordsAsync(int dbNumber, int startOffset, short[] values, CancellationToken ct = default) => WriteMultipleRegistersAsync((byte)1, (ushort)(startOffset / 2), values, ct);
    private TcpClient? _tcpClient;
    private NetworkStream? _stream;
    private ushort _transactionId;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private string _currentIp = "127.0.0.1";
    private int _currentPort = 5020;

    public bool IsConnected => _tcpClient != null && _tcpClient.Connected;
    public event Action<RawFrameLog>? OnRawFrameLogged;

    public async Task<bool> ConnectAsync(string ip, int port, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            DisconnectInternal();
            _currentIp = ip;
            _currentPort = port;

            _tcpClient = new TcpClient();
            _tcpClient.SendTimeout = 2000;
            _tcpClient.ReceiveTimeout = 2000;

            using var timeoutCts = new CancellationTokenSource(3000);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

            await _tcpClient.ConnectAsync(ip, port, linkedCts.Token);
            _stream = _tcpClient.GetStream();
            return true;
        }
        catch
        {
            DisconnectInternal();
            return false;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task DisconnectAsync()
    {
        await _lock.WaitAsync();
        try
        {
            DisconnectInternal();
        }
        finally
        {
            _lock.Release();
        }
    }

    private void DisconnectInternal()
    {
        _stream?.Dispose();
        _stream = null;
        _tcpClient?.Dispose();
        _tcpClient = null;
    }

    /// <summary>
    /// 读取保持寄存器 (功能码 0x03)
    /// </summary>
    public async Task<short[]> ReadHoldingRegistersAsync(byte unitId, ushort startAddress, ushort count, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            EnsureConnected();
            ushort tid = unchecked(++_transactionId);

            // MBAP Header (7B) + PDU (5B) = 12 Bytes
            byte[] request = new byte[12];
            request[0] = (byte)(tid >> 8);
            request[1] = (byte)(tid & 0xFF);
            request[2] = 0; request[3] = 0; // Protocol ID = 0 (Modbus)
            request[4] = 0; request[5] = 6; // Length = 6
            request[6] = unitId;           // Unit ID
            request[7] = 0x03;             // Function Code
            request[8] = (byte)(startAddress >> 8);
            request[9] = (byte)(startAddress & 0xFF);
            request[10] = (byte)(count >> 8);
            request[11] = (byte)(count & 0xFF);

            EmitLog(true, request, $"FC03 读保持寄存器 Addr:{startAddress} Count:{count}");
            await _stream!.WriteAsync(request, 0, request.Length, ct);

            // 接收响应 MBAP 头 (7B) + PDU (2B + count*2 B)
            byte[] header = new byte[7];
            await ReadExactBytesAsync(header, 0, 7, ct);

            ushort length = (ushort)((header[4] << 8) | header[5]);
            byte[] pdu = new byte[length - 1]; // 排除 UnitID
            await ReadExactBytesAsync(pdu, 0, pdu.Length, ct);

            byte[] fullResp = new byte[7 + pdu.Length];
            Array.Copy(header, fullResp, 7);
            Array.Copy(pdu, 0, fullResp, 7, pdu.Length);
            EmitLog(false, fullResp, "FC03 响应数据帧");

            byte funcCode = pdu[0];
            if ((funcCode & 0x80) != 0)
            {
                byte exceptionCode = pdu[1];
                throw new InvalidOperationException($"Modbus 异常响应: 功能码 0x{funcCode:X2}, 错误码 0x{exceptionCode:X2}");
            }

            byte byteCount = pdu[1];
            short[] result = new short[count];
            for (int i = 0; i < count; i++)
            {
                int offset = 2 + i * 2;
                result[i] = (short)((pdu[offset] << 8) | pdu[offset + 1]);
            }
            return result;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// 写单个保持寄存器 (功能码 0x06)
    /// </summary>
    public async Task WriteHoldingRegisterAsync(byte unitId, ushort address, short value, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            EnsureConnected();
            ushort tid = unchecked(++_transactionId);

            byte[] request = new byte[12];
            request[0] = (byte)(tid >> 8);
            request[1] = (byte)(tid & 0xFF);
            request[2] = 0; request[3] = 0;
            request[4] = 0; request[5] = 6;
            request[6] = unitId;
            request[7] = 0x06;
            request[8] = (byte)(address >> 8);
            request[9] = (byte)(address & 0xFF);
            request[10] = (byte)((ushort)value >> 8);
            request[11] = (byte)((ushort)value & 0xFF);

            EmitLog(true, request, $"FC06 写单个寄存器 Addr:{address} Val:{value}");
            await _stream!.WriteAsync(request, 0, request.Length, ct);

            byte[] response = new byte[12];
            await ReadExactBytesAsync(response, 0, 12, ct);
            EmitLog(false, response, "FC06 写寄存器回显");
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// 写多个保持寄存器 (功能码 0x10)
    /// </summary>
    public async Task WriteMultipleRegistersAsync(byte unitId, ushort startAddress, short[] values, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            EnsureConnected();
            ushort tid = unchecked(++_transactionId);
            byte byteCount = (byte)(values.Length * 2);
            ushort pduLength = (ushort)(7 + byteCount);

            byte[] request = new byte[6 + pduLength];
            request[0] = (byte)(tid >> 8);
            request[1] = (byte)(tid & 0xFF);
            request[2] = 0; request[3] = 0;
            request[4] = (byte)(pduLength >> 8);
            request[5] = (byte)(pduLength & 0xFF);
            request[6] = unitId;
            request[7] = 0x10;
            request[8] = (byte)(startAddress >> 8);
            request[9] = (byte)(startAddress & 0xFF);
            request[10] = (byte)(values.Length >> 8);
            request[11] = (byte)(values.Length & 0xFF);
            request[12] = byteCount;

            for (int i = 0; i < values.Length; i++)
            {
                request[13 + i * 2] = (byte)((ushort)values[i] >> 8);
                request[14 + i * 2] = (byte)((ushort)values[i] & 0xFF);
            }

            EmitLog(true, request, $"FC10 批量下发寄存器 Addr:{startAddress} Count:{values.Length}");
            await _stream!.WriteAsync(request, 0, request.Length, ct);

            byte[] response = new byte[12];
            await ReadExactBytesAsync(response, 0, 12, ct);
            EmitLog(false, response, "FC10 批量写响应");
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// 写单个线圈 (功能码 0x05)
    /// </summary>
    public async Task WriteSingleCoilAsync(byte unitId, ushort address, bool value, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            EnsureConnected();
            ushort tid = unchecked(++_transactionId);

            byte[] request = new byte[12];
            request[0] = (byte)(tid >> 8);
            request[1] = (byte)(tid & 0xFF);
            request[2] = 0; request[3] = 0;
            request[4] = 0; request[5] = 6;
            request[6] = unitId;
            request[7] = 0x05;
            request[8] = (byte)(address >> 8);
            request[9] = (byte)(address & 0xFF);
            request[10] = (byte)(value ? 0xFF : 0x00);
            request[11] = 0x00;

            EmitLog(true, request, $"FC05 写线圈 Addr:{address} Val:{value}");
            await _stream!.WriteAsync(request, 0, request.Length, ct);

            byte[] response = new byte[12];
            await ReadExactBytesAsync(response, 0, 12, ct);
            EmitLog(false, response, "FC05 写线圈回显");
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// 读取线圈 (功能码 0x01)
    /// </summary>
    public async Task<bool[]> ReadCoilsAsync(byte unitId, ushort startAddress, ushort count, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            EnsureConnected();
            ushort tid = unchecked(++_transactionId);

            byte[] request = new byte[12];
            request[0] = (byte)(tid >> 8);
            request[1] = (byte)(tid & 0xFF);
            request[2] = 0; request[3] = 0;
            request[4] = 0; request[5] = 6;
            request[6] = unitId;
            request[7] = 0x01;
            request[8] = (byte)(startAddress >> 8);
            request[9] = (byte)(startAddress & 0xFF);
            request[10] = (byte)(count >> 8);
            request[11] = (byte)(count & 0xFF);

            EmitLog(true, request, $"FC01 读线圈 Addr:{startAddress} Count:{count}");
            await _stream!.WriteAsync(request, 0, request.Length, ct);

            byte[] header = new byte[7];
            await ReadExactBytesAsync(header, 0, 7, ct);

            ushort length = (ushort)((header[4] << 8) | header[5]);
            byte[] pdu = new byte[length - 1];
            await ReadExactBytesAsync(pdu, 0, pdu.Length, ct);

            byte[] fullResp = new byte[7 + pdu.Length];
            Array.Copy(header, fullResp, 7);
            Array.Copy(pdu, 0, fullResp, 7, pdu.Length);
            EmitLog(false, fullResp, "FC01 读线圈响应");

            byte byteCount = pdu[1];
            bool[] result = new bool[count];
            for (int i = 0; i < count; i++)
            {
                int byteIdx = 2 + (i / 8);
                int bitIdx = i % 8;
                if (byteIdx < pdu.Length)
                {
                    result[i] = ((pdu[byteIdx] >> bitIdx) & 1) == 1;
                }
            }
            return result;
        }
        finally
        {
            _lock.Release();
        }
    }

    private void EmitLog(bool isTx, byte[] buffer, string desc)
    {
        string hex = BitConverter.ToString(buffer).Replace("-", " ");
        OnRawFrameLogged?.Invoke(new RawFrameLog(DateTime.Now, isTx, hex, desc));
    }

    private void EnsureConnected()
    {
        if (_tcpClient == null || !_tcpClient.Connected || _stream == null)
        {
            throw new InvalidOperationException("Modbus 通信已断开，请检查连接状态");
        }
    }

    private async Task ReadExactBytesAsync(byte[] buffer, int offset, int count, CancellationToken ct)
    {
        int totalRead = 0;
        while (totalRead < count)
        {
            int read = await _stream!.ReadAsync(buffer, offset + totalRead, count - totalRead, ct);
            if (read == 0)
            {
                throw new IOException("Socket 远端已关闭连接");
            }
            totalRead += read;
        }
    }

    public void Dispose()
    {
        DisconnectInternal();
        _lock.Dispose();
    }
}


