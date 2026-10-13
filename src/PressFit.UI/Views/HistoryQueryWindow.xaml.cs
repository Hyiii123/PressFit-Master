using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Win32;
using PressFit.Core.Algorithms;
using PressFit.Core.Enums;
using PressFit.Core.Interfaces;
using PressFit.Core.Models;
using PressFit.Data.Export;

namespace PressFit.UI.Views;

public partial class HistoryQueryWindow : Window
{
    private readonly IPressFitRepository _repo;
    private List<PressResultRecord> _currentRecords = new();

    private readonly Polyline _curveLine = new() { Stroke = new SolidColorBrush(Color.FromRgb(0, 210, 211)), StrokeThickness = 2.0 };
    private readonly Polyline _upperEnvelopeLine = new() { Stroke = new SolidColorBrush(Color.FromRgb(225, 112, 85)), StrokeThickness = 1.2, StrokeDashArray = new DoubleCollection { 4, 3 } };
    private readonly Polyline _lowerEnvelopeLine = new() { Stroke = new SolidColorBrush(Color.FromRgb(9, 132, 227)), StrokeThickness = 1.2, StrokeDashArray = new DoubleCollection { 4, 3 } };

    public HistoryQueryWindow(IPressFitRepository repo)
    {
        InitializeComponent();
        _repo = repo;

        ReplayCanvas.Children.Add(_upperEnvelopeLine);
        ReplayCanvas.Children.Add(_lowerEnvelopeLine);
        ReplayCanvas.Children.Add(_curveLine);

        DpStart.SelectedDate = DateTime.Today.AddDays(-7);
        DpEnd.SelectedDate = DateTime.Today.AddDays(1);

        Loaded += async (s, e) => await ExecuteQueryAsync();
    }

    private async Task ExecuteQueryAsync()
    {
        DateTime start = DpStart.SelectedDate ?? DateTime.Today.AddDays(-7);
        DateTime end = (DpEnd.SelectedDate ?? DateTime.Today).AddDays(1);
        string? barcode = string.IsNullOrWhiteSpace(TxtBarcodeFilter.Text) ? null : TxtBarcodeFilter.Text.Trim();

        var list = await _repo.QueryRecordsAsync(start, end, barcode, limit: 300);
        _currentRecords = list.ToList();
        RecordGrid.ItemsSource = _currentRecords;
        TxtRecordCount.Text = $"生产履历台账 (检索到 {_currentRecords.Count} 条记录)";

        if (_currentRecords.Count > 0)
        {
            RecordGrid.SelectedIndex = 0;
        }
    }

    private async void Search_Click(object sender, RoutedEventArgs e)
    {
        await ExecuteQueryAsync();
    }

    private async void Reset_Click(object sender, RoutedEventArgs e)
    {
        DpStart.SelectedDate = DateTime.Today.AddDays(-7);
        DpEnd.SelectedDate = DateTime.Today.AddDays(1);
        TxtBarcodeFilter.Text = string.Empty;
        await ExecuteQueryAsync();
    }

    private void RecordGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RecordGrid.SelectedItem is not PressResultRecord record) return;

        TxtReplayDetail.Text = $"工件条码: {record.WorkpieceBarcode} | 配方: {record.RecipeCode} | 最终判定: {record.Quality} | 压装时间: {record.CreatedAt:yyyy-MM-dd HH:mm:ss}";
        TxtFailureMsg.Text = string.IsNullOrWhiteSpace(record.FailureReason) ? "工艺合格，未触发超限" : record.FailureReason;
        TxtFailureMsg.Foreground = record.Quality == QualityStatus.OK ? new SolidColorBrush(Color.FromRgb(0, 184, 148)) : new SolidColorBrush(Color.FromRgb(214, 48, 49));

        ReplayCurve(record);
    }

    private void ReplayCurve(PressResultRecord record)
    {
        _curveLine.Points.Clear();
        _upperEnvelopeLine.Points.Clear();
        _lowerEnvelopeLine.Points.Clear();

        if (string.IsNullOrWhiteSpace(record.CurvePointsJson)) return;

        List<PressPoint>? points = null;
        try
        {
            points = JsonSerializer.Deserialize<List<PressPoint>>(record.CurvePointsJson);
        }
        catch { }

        if (points == null || points.Count == 0) return;

        double canvasW = ReplayCanvas.ActualWidth;
        double canvasH = ReplayCanvas.ActualHeight;
        if (canvasW <= 10 || canvasH <= 10)
        {
            canvasW = 450;
            canvasH = 320;
        }

        double maxDisp = Math.Max(25.0, record.FinalDisplacement * 1.15);
        double maxForce = Math.Max(15.0, record.PeakForce * 1.35);

        // 重绘轨迹
        foreach (var pt in points)
        {
            double x = (pt.Displacement / maxDisp) * canvasW;
            double y = canvasH - (pt.Force / maxForce) * canvasH;
            _curveLine.Points.Add(new Point(x, y));
        }

        // 重绘参考包络线
        var recipe = new PressRecipe
        {
            TargetDisplacement = record.FinalDisplacement,
            MaxForceLimit = Math.Max(12.0, record.PeakForce * 1.1),
            MinForceLimit = Math.Max(4.0, record.PeakForce * 0.6),
            EnvelopeMarginPercentage = 10.0
        };

        double marginFactor = 0.10;
        for (int i = 0; i <= 50; i++)
        {
            double disp = (recipe.TargetDisplacement / 50) * i;
            double baseForce = EnvelopeDetector.CalculateTheoreticalForce(disp, recipe);
            double upper = baseForce * (1.0 + marginFactor) + 0.5;
            double lower = Math.Max(0.1, baseForce * (1.0 - marginFactor) - 0.5);

            double x = (disp / maxDisp) * canvasW;
            double yUpper = canvasH - (upper / maxForce) * canvasH;
            double yLower = canvasH - (lower / maxForce) * canvasH;

            _upperEnvelopeLine.Points.Add(new Point(x, yUpper));
            _lowerEnvelopeLine.Points.Add(new Point(x, yLower));
        }
    }

    private async void ExportCsv_Click(object sender, RoutedEventArgs e)
    {
        if (_currentRecords.Count == 0)
        {
            MessageBox.Show("当前无数据可导出！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "导出生产数据质检报表",
            Filter = "CSV 文件 (*.csv)|*.csv",
            FileName = $"PressFit_Report_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                await CsvReportExporter.ExportToCsvAsync(dialog.FileName, _currentRecords);
                MessageBox.Show($"报表已成功导出至:\n{dialog.FileName}", "导出完成", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导出失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
