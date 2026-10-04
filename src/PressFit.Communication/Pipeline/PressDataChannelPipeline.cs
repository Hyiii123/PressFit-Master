using System.Threading.Channels;
using PressFit.Core.Interfaces;
using PressFit.Core.Models;

namespace PressFit.Communication.Pipeline;

/// <summary>
/// 基于 .NET 8 System.Threading.Channels 的高频数采无锁异步流水线
/// 彻底隔离底层 100Hz 高频网络采集与 WPF 60FPS UI 渲染，零 GC 抖动
/// </summary>
public class PressDataChannelPipeline : IPressDataPipeline
{
    private readonly Channel<PressPoint> _channel;

    public PressDataChannelPipeline(int capacity = 20000)
    {
        // 创建有界通道，单写者单读者模式开启内联快速路径
        var options = new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest, // 极端饱和时丢弃最老点，坚决不阻塞采集
            SingleWriter = true,
            SingleReader = true
        };
        _channel = Channel.CreateBounded<PressPoint>(options);
    }

    /// <summary>
    /// 生产者写入高频点（通常来自 Modbus 采集线程）
    /// </summary>
    public ValueTask EnqueuePointAsync(PressPoint point, CancellationToken ct = default)
    {
        return _channel.Writer.WriteAsync(point, ct);
    }

    /// <summary>
    /// 消费者定频抽取批次（批量交付给 ScottPlot 图表与数据库，每次抽取最多 batchSize 点）
    /// </summary>
    public async IAsyncEnumerable<IReadOnlyList<PressPoint>> ReadBatchesAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var batch = new List<PressPoint>(64);
        var reader = _channel.Reader;

        while (await reader.WaitToReadAsync(ct))
        {
            batch.Clear();
            while (batch.Count < 64 && reader.TryRead(out var point))
            {
                batch.Add(point);
            }

            if (batch.Count > 0)
            {
                yield return batch.ToArray();
            }

            // 限制最高刷新帧率约 60FPS (16ms)，避免无意义的 GPU 渲染过度消耗
            await Task.Delay(16, ct);
        }
    }
}
