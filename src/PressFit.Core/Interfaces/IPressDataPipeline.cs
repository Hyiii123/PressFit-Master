using PressFit.Core.Models;

namespace PressFit.Core.Interfaces;

/// <summary>
/// 高性能数采异步流水线接口（基于 Channels 解耦）
/// </summary>
public interface IPressDataPipeline
{
    ValueTask EnqueuePointAsync(PressPoint point, CancellationToken ct = default);
    IAsyncEnumerable<IReadOnlyList<PressPoint>> ReadBatchesAsync(CancellationToken ct = default);
}
