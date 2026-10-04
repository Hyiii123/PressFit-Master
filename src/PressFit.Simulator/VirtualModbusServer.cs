using System.Net;
using System.Net.Sockets;
using PressFit.Communication.Modbus;
using PressFit.Core.Enums;
using PressFit.Core.Interfaces;
using PressFit.Core.Models;
using PressFit.Simulator.Faults;
using PressFit.Simulator.Physics;

namespace PressFit.Simulator;

/// <summary>
/// 高保真工业级虚拟从机 (Virtual Modbus Slave Server)
/// 监听 127.0.0.1:5020，真实模拟伺服控制器与传感器行为，支持随时注入通信与业务故障
/// </summary>
public class VirtualModbusServer : IVirtualSlaveService, IDisposable
{
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private readonly short[] _holdingRegisters = new short[32];
    private readonly bool[] _coils = new bool[16];
    private readonly object _lock = new();

    private PressRecipe _currentRecipe = new()
    {
        TargetDisplacement = 28.5,
        MaxForceLimit = 18.0,
        MinForceLimit = 10.0,
        ApproachSpeed = 40.0,
        PressSpeed = 4.0,
        HoldingTimeSec = 1.0
    };

    public FaultInjectionConfig Faults { get; } = new();
    public bool IsRunning { get; private set; }
    public int ListeningPort { get; private set; } = 5020;

    public Task StartAsync(int port = 5020, CancellationToken ct = default)
    {
        ListeningPort = port;
        _cts = new CancellationTokenSource();
        _listener = new TcpListener(IPAddress.Loopback, port);
        _listener.Start();
        IsRunning = true;

        // 初始化默认待机状态
        lock (_lock)
        {
            _holdingRegisters[ModbusAddressMap.REG_MACHINE_STATE] = (short)MachineState.Idle;
            _holdingRegisters[ModbusAddressMap.REG_PRESS_PHASE] = (short)PressPhase.Standby;
            _holdingRegisters[ModbusAddressMap.REG_RECIPE_TARGET_DISP] = (short)(_currentRecipe.TargetDisplacement * 100);
            _holdingRegisters[ModbusAddressMap.REG_RECIPE_MAX_FORCE] = (short)(_currentRecipe.MaxForceLimit * 100);
            _holdingRegisters[ModbusAddressMap.REG_RECIPE_MIN_FORCE] = (short)(_currentRecipe.MinForceLimit * 100);
        }

        Task.Run(() => AcceptClientsLoopAsync(_cts.Token));
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        IsRunning = false;
        _cts?.Cancel();
        _listener?.Stop();
        _listener = null;
        return Task.CompletedTask;
    }

    public void SetRecipe(PressRecipe recipe)
    {
        lock (_lock)
        {
            _currentRecipe = recipe;
            _holdingRegisters[ModbusAddressMap.REG_RECIPE_TARGET_DISP] = (short)(recipe.TargetDisplacement * 100);
            _holdingRegisters[ModbusAddressMap.REG_RECIPE_MAX_FORCE] = (short)(recipe.MaxForceLimit * 100);
            _holdingRegisters[ModbusAddressMap.REG_RECIPE_MIN_FORCE] = (short)(recipe.MinForceLimit * 100);
        }
    }

    public void TriggerCycleStart(string barcode)
    {
        Task.Run(RunSimulatedCycleAsync);
    }

    public void TriggerEmergencyStop()
    {
        lock (_lock)
        {
            _holdingRegisters[ModbusAddressMap.REG_MACHINE_STATE] = (short)MachineState.EmergencyStop;
            _coils[ModbusAddressMap.COIL_STATUS_EMERGENCY] = true;
        }
    }

    public void InjectFault(string faultType, bool enabled)
    {
        switch (faultType)
        {
            case "Disconnect": Faults.SimulateNetworkDisconnect = enabled; break;
            case "Timeout": Faults.SimulatePacketTimeout = enabled; break;
            case "ForceSpike": Faults.SimulateForceSpikeNG = enabled; break;
            case "ForceLow": Faults.SimulateForceLowNG = enabled; break;
            case "DispShort": Faults.SimulateDisplacementShortNG = enabled; break;
            case "ServoAlarm":
                Faults.SimulateServoAlarm = enabled;
                lock (_lock)
                {
                    if (enabled)
                    {
                        _holdingRegisters[ModbusAddressMap.REG_MACHINE_STATE] = (short)MachineState.Alarm;
                        _holdingRegisters[ModbusAddressMap.REG_ALARM_CODE] = 0x5001; // 伺服驱动器过载代码
                    }
                    else
                    {
                        _holdingRegisters[ModbusAddressMap.REG_MACHINE_STATE] = (short)MachineState.Idle;
                        _holdingRegisters[ModbusAddressMap.REG_ALARM_CODE] = 0;
                    }
                }
                break;
        }
    }

    private async Task AcceptClientsLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener != null)
        {
            try
            {
                var client = await _listener.AcceptTcpClientAsync(ct);
                _ = Task.Run(() => HandleClientAsync(client, ct), ct);
            }
            catch when (ct.IsCancellationRequested) { break; }
            catch { }
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
    {
        using var stream = client.GetStream();
        byte[] buffer = new byte[256];

        while (!ct.IsCancellationRequested && client.Connected)
        {
            // 故障注入：模拟断网
            if (Faults.SimulateNetworkDisconnect)
            {
                client.Close();
                break;
            }

            int read = await stream.ReadAsync(buffer, 0, buffer.Length, ct);
            if (read == 0) break;

            // 故障注入：模拟响应超时 (3.5秒延时)
            if (Faults.SimulatePacketTimeout)
            {
                await Task.Delay(3500, ct);
            }

            if (read >= 12)
            {
                byte func = buffer[7];
                ushort addr = (ushort)((buffer[8] << 8) | buffer[9]);
                ushort count = (ushort)((buffer[10] << 8) | buffer[11]);

                if (func == 0x03) // 读保持寄存器
                {
                    byte byteCount = (byte)(count * 2);
                    byte[] response = new byte[9 + byteCount];
                    Array.Copy(buffer, response, 6);
                    response[4] = 0; response[5] = (byte)(3 + byteCount);
                    response[6] = buffer[6]; // UnitID
                    response[7] = 0x03;
                    response[8] = byteCount;

                    lock (_lock)
                    {
                        for (int i = 0; i < count; i++)
                        {
                            short val = (addr + i < _holdingRegisters.Length) ? _holdingRegisters[addr + i] : (short)0;
                            response[9 + i * 2] = (byte)(val >> 8);
                            response[10 + i * 2] = (byte)(val & 0xFF);
                        }
                    }
                    await stream.WriteAsync(response, 0, response.Length, ct);
                }
                else if (func == 0x05) // 写单个线圈
                {
                    bool coilVal = buffer[10] == 0xFF;
                    lock (_lock)
                    {
                        if (addr < _coils.Length) _coils[addr] = coilVal;
                        if (addr == ModbusAddressMap.COIL_CMD_START && coilVal)
                        {
                            Task.Run(RunSimulatedCycleAsync);
                        }
                    }
                    await stream.WriteAsync(buffer, 0, 12, ct); // 回显
                }
                else if (func == 0x06) // 写单寄存器
                {
                    short regVal = (short)((buffer[10] << 8) | buffer[11]);
                    lock (_lock)
                    {
                        if (addr < _holdingRegisters.Length) _holdingRegisters[addr] = regVal;
                    }
                    await stream.WriteAsync(buffer, 0, 12, ct);
                }
                else if (func == 0x01) // 读线圈
                {
                    byte[] resp = new byte[10];
                    Array.Copy(buffer, resp, 6);
                    resp[4] = 0; resp[5] = 4;
                    resp[6] = buffer[6];
                    resp[7] = 0x01;
                    resp[8] = 1;
                    byte bits = 0;
                    lock (_lock)
                    {
                        for (int i = 0; i < Math.Min((int)count, 8); i++)
                        {
                            if (addr + i < _coils.Length && _coils[addr + i]) bits |= (byte)(1 << i);
                        }
                    }
                    resp[9] = bits;
                    await stream.WriteAsync(resp, 0, 10, ct);
                }
            }
        }
    }

    private async Task RunSimulatedCycleAsync()
    {
        lock (_lock)
        {
            _holdingRegisters[ModbusAddressMap.REG_MACHINE_STATE] = (short)MachineState.Running;
            _coils[ModbusAddressMap.COIL_STATUS_CYCLE_DONE] = false;
        }

        var generator = new PressCurveGenerator();
        var trajectory = generator.GenerateTrajectory(_currentRecipe, Faults);

        double peak = 0;
        foreach (var p in trajectory)
        {
            if (p.Force > peak) peak = p.Force;

            lock (_lock)
            {
                _holdingRegisters[ModbusAddressMap.REG_DISPLACEMENT_SCALED] = (short)(p.Displacement * 100);
                _holdingRegisters[ModbusAddressMap.REG_FORCE_SCALED] = (short)(p.Force * 100);
                _holdingRegisters[ModbusAddressMap.REG_PEAK_FORCE_SCALED] = (short)(peak * 100);
                _holdingRegisters[ModbusAddressMap.REG_CYCLE_TIME_MS] = (short)p.TimestampMs;

                if (p.Displacement < 2.0)
                    _holdingRegisters[ModbusAddressMap.REG_PRESS_PHASE] = (short)PressPhase.RapidApproach;
                else if (p.Displacement < _currentRecipe.TargetDisplacement)
                    _holdingRegisters[ModbusAddressMap.REG_PRESS_PHASE] = (short)PressPhase.ControlledPress;
                else
                    _holdingRegisters[ModbusAddressMap.REG_PRESS_PHASE] = (short)PressPhase.ForceHold;
            }

            await Task.Delay(20); // 50Hz 物理推进
        }

        // 周期结束判定
        lock (_lock)
        {
            _holdingRegisters[ModbusAddressMap.REG_MACHINE_STATE] = (short)MachineState.Idle;
            _holdingRegisters[ModbusAddressMap.REG_PRESS_PHASE] = (short)PressPhase.Completed;
            _coils[ModbusAddressMap.COIL_STATUS_CYCLE_DONE] = true;
            _coils[ModbusAddressMap.COIL_CMD_START] = false;
        }
    }

    public void Dispose()
    {
        StopAsync();
        _cts?.Dispose();
    }
}
