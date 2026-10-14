using System.Net.Sockets;
using PressFit.Core.Enums;
using PressFit.Core.Interfaces;
using PressFit.Core.Models;

namespace PressFit.Communication.Drivers;

/// <summary>
/// 西门子 S7-1200 / S7-1500 原生以太网通信驱动 (ISO-on-TCP RFC 1006 / S7 PDU)
/// 专用于读取西门子 PLC 内部 DB (Data Block) 数据块
/// </summary>
public class SiemensS7Driver : IIndustrialPlcDriver, IDisposable
{
    private TcpClient? _tcpClient;
    private NetworkStream? _stream;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public PlcProtocolType ProtocolType => PlcProtocolType.SiemensS7;
    public bool IsConnected => _tcpClient != null && _tcpClient.Connected;
    public event Action<RawFrameLog>? OnRawFrameLogged;

    public int Rack { get; set; } = 0;
    public int Slot { get; set; } = 1; // S7-1200/1500 为 1; S7-300 为 2
    public int DefaultDbNumber { get; set; } = 1; // 默认数据块 DB1

    public async Task<bool> ConnectAsync(string ip, int port = 102, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            DisconnectInternal();
            _tcpClient = new TcpClient();
            _tcpClient.SendTimeout = 2000;
            _tcpClient.ReceiveTimeout = 2000;

            // 西门子 S7 专用 ISO-on-TCP 端口为 102 (若传 5020 则为模拟测试端口)
            await _tcpClient.ConnectAsync(ip, port, ct);
            _stream = _tcpClient.GetStream();

            // 1. 发送 COTP 连接请求报文 (Connection Request, RFC 1006)
            byte[] cotpCr = BuildCotpConnectionRequest(Rack, Slot);
            EmitLog(true, cotpCr, "S7 COTP 连接请求 (RFC 1006)");
            await _stream.WriteAsync(cotpCr, 0, cotpCr.Length, ct);

            byte[] cotpCc = new byte[22];
            await ReadExactBytesAsync(cotpCc, 0, cotpCc.Length, ct);
            EmitLog(false, cotpCc, "S7 COTP 连接确认 (CC 响应)");

            // 2. 发送 S7 通信协商初始化 PDU (Setup Communication PDU)
            byte[] s7Setup = BuildS7SetupCommunicationPdu();
            EmitLog(true, s7Setup, "S7 通信参数初始化握手 (Setup Comm)");
            await _stream.WriteAsync(s7Setup, 0, s7Setup.Length, ct);

            byte[] s7SetupResp = new byte[27];
            await ReadExactBytesAsync(s7SetupResp, 0, s7SetupResp.Length, ct);
            EmitLog(false, s7SetupResp, "S7 握手确认 (S7 Comm OK)");

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
    /// 读取西门子 S7 数据块 DB 中的连续 Word (16位) 数据
    /// </summary>
    public async Task<short[]> ReadDbWordsAsync(int dbNumber, int startOffset, int count, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            EnsureConnected();
            int byteLength = count * 2;
            byte[] readReq = BuildS7ReadRequest(dbNumber, startOffset, byteLength);

            EmitLog(true, readReq, $"S7 读取 DB{dbNumber}.DBW{startOffset} Count:{count}");
            await _stream!.WriteAsync(readReq, 0, readReq.Length, ct);

            // S7 读取响应报文头 (21 字节) + 数据区 (byteLength 字节)
            int expectedLen = 21 + byteLength;
            byte[] response = new byte[expectedLen];
            await ReadExactBytesAsync(response, 0, expectedLen, ct);
            EmitLog(false, response, $"S7 DB{dbNumber} 读取响应");

            short[] result = new short[count];
            int dataStart = 25; // S7 数据起始位置 (RFC1006 4B + S7 Header 10B + Param 2B + Return 4B + Data)
            if (response.Length >= 25 + byteLength)
            {
                dataStart = 25;
            }
            else
            {
                dataStart = 21;
            }

            for (int i = 0; i < count; i++)
            {
                int idx = dataStart + i * 2;
                if (idx + 1 < response.Length)
                {
                    // 西门子大端字节序 (Big-Endian)
                    result[i] = (short)((response[idx] << 8) | response[idx + 1]);
                }
            }
            return result;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// 批量写入西门子 S7 DB 块数据
    /// </summary>
    public async Task WriteDbWordsAsync(int dbNumber, int startOffset, short[] values, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            EnsureConnected();
            byte[] writeReq = BuildS7WriteRequest(dbNumber, startOffset, values);
            EmitLog(true, writeReq, $"S7 写入 DB{dbNumber}.DBW{startOffset} Count:{values.Length}");
            await _stream!.WriteAsync(writeReq, 0, writeReq.Length, ct);

            byte[] response = new byte[22];
            await ReadExactBytesAsync(response, 0, response.Length, ct);
            EmitLog(false, response, $"S7 DB{dbNumber} 写入回显确认");
        }
        finally
        {
            _lock.Release();
        }
    }

    // ================= 兼容 IModbusMasterService 接口封装 =================
    public Task<short[]> ReadHoldingRegistersAsync(byte unitId, ushort startAddress, ushort count, CancellationToken ct = default)
    {
        // 将 Modbus 寄存器地址映射至西门子 S7 的 DB 块偏移量: DBW[startAddress * 2]
        return ReadDbWordsAsync(DefaultDbNumber, startAddress * 2, count, ct);
    }

    public async Task WriteHoldingRegisterAsync(byte unitId, ushort address, short value, CancellationToken ct = default)
    {
        await WriteDbWordsAsync(DefaultDbNumber, address * 2, new[] { value }, ct);
    }

    public Task WriteMultipleRegistersAsync(byte unitId, ushort startAddress, short[] values, CancellationToken ct = default)
    {
        return WriteDbWordsAsync(DefaultDbNumber, startAddress * 2, values, ct);
    }

    public Task WriteSingleCoilAsync(byte unitId, ushort address, bool value, CancellationToken ct = default)
    {
        // 线圈映射为 DBB0 的特定位或 Word
        return WriteDbWordsAsync(DefaultDbNumber, 100 + address * 2, new[] { (short)(value ? 1 : 0) }, ct);
    }

    public async Task<bool[]> ReadCoilsAsync(byte unitId, ushort startAddress, ushort count, CancellationToken ct = default)
    {
        var words = await ReadDbWordsAsync(DefaultDbNumber, 100 + startAddress * 2, count, ct);
        return words.Select(w => w != 0).ToArray();
    }

    // ================= S7 底层报文构造辅助 =================
    private static byte[] BuildCotpConnectionRequest(int rack, int slot)
    {
        byte dstTsap2 = (byte)((rack * 0x20) + slot);
        return new byte[]
        {
            0x03, 0x00, 0x00, 0x16, // RFC 1006 Header (Len = 22)
            0x11,                   // COTP Length
            0xE0,                   // Connection Request
            0x00, 0x00,             // Dest Ref
            0x00, 0x01,             // Src Ref
            0x00,                   // Class
            0xC1, 0x02, 0x01, 0x00, // Src TSAP
            0xC2, 0x02, 0x01, dstTsap2, // Dst TSAP (包含机架号和槽位号)
            0xC0, 0x01, 0x0A        // TPDU Size
        };
    }

    private static byte[] BuildS7SetupCommunicationPdu()
    {
        return new byte[]
        {
            0x03, 0x00, 0x00, 0x19, // TPKT (Len = 25)
            0x02, 0xF0, 0x80,       // COTP Data
            0x32, 0x01, 0x00, 0x00, // S7 Protocol Id (0x32), Job
            0x00, 0x00,             // Redundancy Identification
            0x00, 0x08,             // Param Length = 8
            0x00, 0x00,             // Data Length = 0
            0xF0, 0x00,             // Function: Setup Comm
            0x00, 0x01,             // Max AmQ Calling
            0x00, 0x01,             // Max AmQ Called
            0x01, 0xE0              // PDU Length = 480
        };
    }

    private static byte[] BuildS7ReadRequest(int dbNumber, int startOffset, int byteCount)
    {
        int bitOffset = startOffset * 8;
        return new byte[]
        {
            0x03, 0x00, 0x00, 0x1F, // TPKT Length = 31
            0x02, 0xF0, 0x80,       // COTP
            0x32, 0x01, 0x00, 0x00, 0x00, 0x01, // S7 Header
            0x00, 0x0E, 0x00, 0x00, // Param Len = 14
            0x04, 0x01,             // Function = 0x04 (Read Var), Item Count = 1
            0x12, 0x0A, 0x10,       // Syntax Id: S7 Any Pointer
            0x02,                   // Transport Size: Byte (0x02)
            (byte)(byteCount >> 8), (byte)(byteCount & 0xFF), // Length
            (byte)(dbNumber >> 8), (byte)(dbNumber & 0xFF),   // DB Number
            0x84,                   // Area: DB (0x84)
            (byte)(bitOffset >> 16), (byte)(bitOffset >> 8), (byte)(bitOffset & 0xFF) // 3B 偏移量 (bit)
        };
    }

    private static byte[] BuildS7WriteRequest(int dbNumber, int startOffset, short[] values)
    {
        int byteCount = values.Length * 2;
        int bitOffset = startOffset * 8;
        int totalLen = 35 + byteCount;

        byte[] pdu = new byte[totalLen];
        byte[] header = new byte[]
        {
            0x03, 0x00, (byte)(totalLen >> 8), (byte)(totalLen & 0xFF),
            0x02, 0xF0, 0x80,
            0x32, 0x01, 0x00, 0x00, 0x00, 0x02,
            0x00, 0x0E, (byte)((4 + byteCount) >> 8), (byte)((4 + byteCount) & 0xFF),
            0x05, 0x01, // Write Var
            0x12, 0x0A, 0x10, 0x02,
            (byte)(byteCount >> 8), (byte)(byteCount & 0xFF),
            (byte)(dbNumber >> 8), (byte)(dbNumber & 0xFF),
            0x84,
            (byte)(bitOffset >> 16), (byte)(bitOffset >> 8), (byte)(bitOffset & 0xFF),
            0x00, 0x04, (byte)((byteCount * 8) >> 8), (byte)((byteCount * 8) & 0xFF)
        };

        Array.Copy(header, pdu, 35);
        for (int i = 0; i < values.Length; i++)
        {
            pdu[35 + i * 2] = (byte)((ushort)values[i] >> 8);
            pdu[35 + i * 2 + 1] = (byte)((ushort)values[i] & 0xFF);
        }
        return pdu;
    }

    private void EmitLog(bool isTx, byte[] buffer, string desc)
    {
        string hex = BitConverter.ToString(buffer).Replace("-", " ");
        OnRawFrameLogged?.Invoke(new RawFrameLog(DateTime.Now, isTx, hex, desc));
    }

    private void EnsureConnected()
    {
        if (_tcpClient == null || !_tcpClient.Connected || _stream == null)
            throw new InvalidOperationException("西门子 S7 通信链路未连接！");
    }

    private async Task ReadExactBytesAsync(byte[] buffer, int offset, int count, CancellationToken ct)
    {
        int total = 0;
        while (total < count)
        {
            int read = await _stream!.ReadAsync(buffer, offset + total, count - total, ct);
            if (read == 0) throw new IOException("西门子 S7 连接被远端重置");
            total += read;
        }
    }

    public void Dispose()
    {
        DisconnectInternal();
        _lock.Dispose();
    }
}
