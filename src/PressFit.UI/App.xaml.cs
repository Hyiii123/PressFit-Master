using System.Windows;
using Microsoft.Extensions.DependencyInjection;
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
}
