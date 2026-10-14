using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using PressFit.Communication.Alarm;
using PressFit.Communication.Modbus;
using PressFit.Communication.Pipeline;
using PressFit.Core.Interfaces;
using PressFit.Data.Repositories;
using PressFit.Simulator;
using PressFit.UI.ViewModels;

namespace PressFit.UI;

public partial class App : Application
{
    public static IServiceProvider ServiceProvider { get; private set; } = null!;

    private async void Application_Startup(object sender, StartupEventArgs e)
    {
        var services = new ServiceCollection();

        // 注册通信与流水线组件
        services.AddSingleton<IModbusMasterService, ModbusTcpClientService>();
        services.AddSingleton<IPressDataPipeline, PressDataChannelPipeline>();

        // 注册仓储与数据库
        services.AddSingleton<IPressFitRepository, PressFitRepository>();

        // 注册 ISA-18.2 报警管理服务
        services.AddSingleton<IAlarmManagerService, AlarmManagerService>();

        // 注册虚拟仿真从机
        services.AddSingleton<IVirtualSlaveService, VirtualModbusServer>();

        // 注册 ViewModels 与 Views
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();

        ServiceProvider = services.BuildServiceProvider();

        // 初始化 SQLite 数据库与种子配方
        var repo = ServiceProvider.GetRequiredService<IPressFitRepository>();
        await repo.InitializeAsync();

        var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    /// <summary>
    /// 全局动态切换语言包 (无缝毫秒级热切，无需重启主窗口)
    /// </summary>
    public static void SwitchLanguage(string cultureCode) // "zh-CN" 或 "en-US"
    {
        var dict = new ResourceDictionary
        {
            Source = new Uri($"Languages/Strings.{cultureCode}.xaml", UriKind.Relative)
        };

        var merged = Current.Resources.MergedDictionaries;
        var existing = merged.FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains("Strings."));
        if (existing != null)
        {
            merged.Remove(existing);
        }
        merged.Add(dict);
    }
}
