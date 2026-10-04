using PressFit.Core.Models;

namespace PressFit.Core.Interfaces;

/// <summary>
/// 高保真虚拟从机仿真器接口
/// </summary>
public interface IVirtualSlaveService
{
    bool IsRunning { get; }
    int ListeningPort { get; }
    Task StartAsync(int port = 5020, CancellationToken ct = default);
    Task StopAsync();
    void SetRecipe(PressRecipe recipe);
    void TriggerCycleStart(string barcode);
    void TriggerEmergencyStop();
    void InjectFault(string faultType, bool enabled);
}
