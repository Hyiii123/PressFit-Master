using System.Net.Sockets;
using System.Text.Json;
using PressFit.Core.Models;

namespace PressFit.Communication.Config;

/// <summary>
/// 通信网络参数持久化与现场物理连通性预检服务
/// </summary>
public static class CommunicationConfigManager
{
    private const string ConfigFileName = "communication_config.json";

    public static CommunicationConfig LoadConfig()
    {
        try
        {
            if (File.Exists(ConfigFileName))
            {
                string json = File.ReadAllText(ConfigFileName);
                var config = JsonSerializer.Deserialize<CommunicationConfig>(json);
                if (config != null) return config;
            }
        }
        catch { }

        return new CommunicationConfig();
    }

    public static void SaveConfig(CommunicationConfig config)
    {
        config.LastUpdatedAt = DateTime.Now;
        string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(ConfigFileName, json);
    }

    /// <summary>
    /// 在物理握手前对目标 PLC IP 和端口执行毫秒级 Socket 探针连通性预检
    /// </summary>
    public static async Task<(bool Success, string Message)> TestConnectionAsync(string ip, int port, int timeoutMs = 1500)
    {
        using var client = new TcpClient();
        try
        {
            using var cts = new CancellationTokenSource(timeoutMs);
            await client.ConnectAsync(ip, port, cts.Token);
            return (true, $"物理端口 {ip}:{port} 连通性测试通过！网络延迟极佳。");
        }
        catch (OperationCanceledException)
        {
            return (false, $"连接超时 (超过 {timeoutMs}ms)：目标地址 {ip}:{port} 未响应，请检查网线或防火墙。");
        }
        catch (Exception ex)
        {
            return (false, $"物理连接被拒: {ex.Message}");
        }
    }
}
