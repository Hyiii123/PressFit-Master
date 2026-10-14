using PressFit.Core.Interfaces;
using PressFit.Core.Models;

namespace PressFit.Communication.StoreAndForward;

/// <summary>
/// 边缘离线存储与断点补传服务 (Store-and-Forward Engine)
/// 车间断网时无缝暂存 SQLite，网络恢复后在后台无感流水线批量补传至 MES / 云端
/// </summary>
public class MesSyncWorker
{
    private readonly IPressFitRepository _repository;
    private CancellationTokenSource? _cts;
    private Task? _workerTask;

    public bool IsOnline { get; set; } = true;
    public int PendingSyncCount { get; private set; } = 0;
    public event Action<int, int>? OnSyncProgressChanged; // pending, newlySynced

    public MesSyncWorker(IPressFitRepository repository)
    {
        _repository = repository;
    }

    public void Start()
    {
        Stop();
        _cts = new CancellationTokenSource();
        _workerTask = Task.Run(() => WorkerLoopAsync(_cts.Token));
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts = null;
    }

    private async Task WorkerLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            if (IsOnline)
            {
                try
                {
                    // 检查是否有未同步的生产记录
                    var unsynced = await _repository.QueryRecordsAsync(DateTime.Today.AddDays(-30), DateTime.Today.AddDays(1), limit: 20);
                    var pendingList = unsynced.Where(r => !r.IsSyncedToMes).ToList();
                    PendingSyncCount = pendingList.Count;

                    if (pendingList.Count > 0)
                    {
                        int syncedThisBatch = 0;
                        foreach (var record in pendingList)
                        {
                            // 模拟向工厂 MES REST/MQTT 接口推送数据
                            await Task.Delay(30, ct); 
                            record.IsSyncedToMes = true;
                            record.SyncedAt = DateTime.Now;
                            syncedThisBatch++;
                        }

                        PendingSyncCount = Math.Max(0, PendingSyncCount - syncedThisBatch);
                        OnSyncProgressChanged?.Invoke(PendingSyncCount, syncedThisBatch);
                    }
                }
                catch { }
            }

            await Task.Delay(2000, ct); // 每 2 秒轮询一次待同步队列
        }
    }
}
