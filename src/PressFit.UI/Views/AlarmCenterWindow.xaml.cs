using System.Windows;
using System.Windows.Controls;
using PressFit.Core.Interfaces;

namespace PressFit.UI.Views;

public partial class AlarmCenterWindow : Window
{
    private readonly IAlarmManagerService _alarmService;
    private readonly IPressFitRepository _repo;

    public AlarmCenterWindow(IAlarmManagerService alarmService, IPressFitRepository repo)
    {
        InitializeComponent();
        _alarmService = alarmService;
        _repo = repo;

        _alarmService.OnAlarmsChanged += RefreshActiveAlarms;
        Loaded += async (s, e) =>
        {
            DpStart.SelectedDate = DateTime.Today.AddDays(-7);
            DpEnd.SelectedDate = DateTime.Today.AddDays(1);
            RefreshActiveAlarms();
            await QueryHistoryAlarms();
        };
    }

    private void RefreshActiveAlarms()
    {
        Dispatcher.Invoke(() =>
        {
            var active = _alarmService.ActiveAlarms;
            ActiveAlarmGrid.ItemsSource = null;
            ActiveAlarmGrid.ItemsSource = active;
            TxtActiveCount.Text = $"当前活动报警: {active.Count} 项 (未确认: {_alarmService.UnacknowledgedCount} 项)";
        });
    }

    private async void AckSingle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is long id)
        {
            await _alarmService.AcknowledgeAlarmAsync(id);
        }
    }

    private async void AckAll_Click(object sender, RoutedEventArgs e)
    {
        await _alarmService.AcknowledgeAllAlarmsAsync();
    }

    private async void QueryHistory_Click(object sender, RoutedEventArgs e)
    {
        await QueryHistoryAlarms();
    }

    private async Task QueryHistoryAlarms()
    {
        DateTime start = DpStart.SelectedDate ?? DateTime.Today.AddDays(-7);
        DateTime end = (DpEnd.SelectedDate ?? DateTime.Today).AddDays(1);

        var list = await _repo.QueryAlarmsAsync(start, end, limit: 300);
        HistoryAlarmGrid.ItemsSource = list;
    }

    protected override void OnClosed(EventArgs e)
    {
        _alarmService.OnAlarmsChanged -= RefreshActiveAlarms;
        base.OnClosed(e);
    }
}


