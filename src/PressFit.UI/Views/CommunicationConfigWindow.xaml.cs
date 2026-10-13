using System.Windows;
using System.Windows.Media;
using PressFit.Communication.Config;
using PressFit.Core.Models;

namespace PressFit.UI.Views;

public partial class CommunicationConfigWindow : Window
{
    public CommunicationConfig Config { get; private set; }
    public bool HasApplied { get; private set; } = false;

    public CommunicationConfigWindow(CommunicationConfig currentConfig)
    {
        InitializeComponent();
        Config = currentConfig;

        TxtIp.Text = Config.IpAddress;
        TxtPort.Text = Config.Port.ToString();
        TxtPolling.Text = Config.PollingIntervalMs.ToString();
        TxtTimeout.Text = Config.TimeoutMs.ToString();
        TxtHeartbeat.Text = Config.HeartbeatIntervalMs.ToString();
        ChkReconnect.IsChecked = Config.AutoReconnect;
    }

    private async void PingTest_Click(object sender, RoutedEventArgs e)
    {
        string ip = TxtIp.Text.Trim();
        if (!int.TryParse(TxtPort.Text, out int port))
        {
            TxtPingResult.Text = "端口格式不合法！";
            TxtPingResult.Foreground = new SolidColorBrush(Color.FromRgb(214, 48, 49));
            return;
        }

        TxtPingResult.Text = "正在发送物理探针测试...";
        TxtPingResult.Foreground = new SolidColorBrush(Color.FromRgb(0, 210, 211));

        var (success, msg) = await CommunicationConfigManager.TestConnectionAsync(ip, port, 1500);
        TxtPingResult.Text = msg;
        TxtPingResult.Foreground = success ? new SolidColorBrush(Color.FromRgb(0, 184, 148)) : new SolidColorBrush(Color.FromRgb(214, 48, 49));
    }

    private void SaveConfig_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Config.IpAddress = TxtIp.Text.Trim();
            Config.Port = int.Parse(TxtPort.Text.Trim());
            Config.PollingIntervalMs = int.Parse(TxtPolling.Text.Trim());
            Config.TimeoutMs = int.Parse(TxtTimeout.Text.Trim());
            Config.HeartbeatIntervalMs = int.Parse(TxtHeartbeat.Text.Trim());
            Config.AutoReconnect = ChkReconnect.IsChecked ?? true;

            CommunicationConfigManager.SaveConfig(Config);
            HasApplied = true;
            MessageBox.Show("通信参数已保存并持久化！已同步更新至系统通信线程。", "通知", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"保存失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
