using PressFit.Core.Enums;

namespace PressFit.Core.Interfaces;

/// <summary>
/// 工业多协议下位机统一抽象驱动接口
/// 统一支撑 Modbus TCP 与西门子 S7-1200/1500 双核心协议
/// </summary>
public interface IIndustrialPlcDriver : IModbusMasterService
{
    PlcProtocolType ProtocolType { get; }

    /// <summary>
    /// 读取西门子 S7 DB 块字数据 (针对 S7-1200/1500)
    /// </summary>
    Task<short[]> ReadDbWordsAsync(int dbNumber, int startOffset, int count, CancellationToken ct = default);

    /// <summary>
    /// 批量写入西门子 S7 DB 块字数据
    /// </summary>
    Task WriteDbWordsAsync(int dbNumber, int startOffset, short[] values, CancellationToken ct = default);
}
