namespace PressFit.Core.Models;

/// <summary>
/// 原始报文通信抓包数据帧记录
/// </summary>
public record RawFrameLog(
    DateTime Timestamp,
    bool IsTx,        // true: 发送 (TX), false: 接收 (RX)
    string HexString, // 十六进制格式化字符串 (如 00 01 00 00 00 06 01 03 ...)
    string Description
);
