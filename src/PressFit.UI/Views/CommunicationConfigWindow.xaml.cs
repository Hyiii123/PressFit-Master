using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PressFit.Communication.Config;
using PressFit.Core.Enums;
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

        TxtRack.Text = Config.SiemensRack.ToString();
        TxtSlot.Text = Config.SiemensSlot.ToString();
        TxtDbNo.Text = Config.SiemensDbNumber.ToString();

        CmbProtocol.SelectedIndex = Config.Protocol == PlcProtocolType.SiemensS7 ? 1 : 0;
    }

    private void CmbProtocol_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SiemensSettingsPanel == null || TxtPort == null) return;
        bool isSiemens = CmbProtocol.SelectedIndex == 1;
        SiemensSettingsPanel.Visibility = isSiemens ? Visibility.Visible : Visibility.Collapsed;
        if (isSiemens && (TxtPort.Text == "5020" || TxtPort.Text == "502"))
        {
            TxtPort.Text = "102"; // 西门子标准 S7 端口
        }
        else if (!isSiemens && TxtPort.Text == "102")
        {
            TxtPort.Text = "5020";
        }
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
            Config.Protocol = CmbProtocol.SelectedIndex == 1 ? PlcProtocolType.SiemensS7 : PlcProtocolType.ModbusTcp;
            Config.IpAddress = TxtIp.Text.Trim();
            Config.Port = int.Parse(TxtPort.Text.Trim());
            Config.PollingIntervalMs = int.Parse(TxtPolling.Text.Trim());
            Config.TimeoutMs = int.Parse(TxtTimeout.Text.Trim());

            Config.SiemensRack = int.Parse(TxtRack.Text.Trim());
            Config.SiemensSlot = int.Parse(TxtSlot.Text.Trim());
            Config.SiemensDbNumber = int.Parse(TxtDbNo.Text.Trim());

            CommunicationConfigManager.SaveConfig(Config);
            HasApplied = true;
            MessageBox.Show($"[{Config.Protocol}] 通信参数已保存！系统驱动已更新。", "通知", MessageBoxButton.OK, MessageBoxImage.Information);
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
