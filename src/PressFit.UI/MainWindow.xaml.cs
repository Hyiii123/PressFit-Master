using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using PressFit.Core.Algorithms;
using PressFit.Core.Models;
using PressFit.UI.ViewModels;

namespace PressFit.UI;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly Polyline _liveLine = new() { Stroke = new SolidColorBrush(Color.FromRgb(0, 210, 211)), StrokeThickness = 2.0 };
    private readonly Polyline _upperEnvelopeLine = new() { Stroke = new SolidColorBrush(Color.FromRgb(225, 112, 85)), StrokeThickness = 1.2, StrokeDashArray = new DoubleCollection { 4, 3 } };
    private readonly Polyline _lowerEnvelopeLine = new() { Stroke = new SolidColorBrush(Color.FromRgb(9, 132, 227)), StrokeThickness = 1.2, StrokeDashArray = new DoubleCollection { 4, 3 } };

    public MainWindow(MainViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = _vm;

        CurveCanvas.Children.Add(_upperEnvelopeLine);
        CurveCanvas.Children.Add(_lowerEnvelopeLine);
        CurveCanvas.Children.Add(_liveLine);

        _vm.OnBatchDataReceived += Vm_OnBatchDataReceived;
        _vm.OnCycleResetRequested += Vm_OnCycleResetRequested;
        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        RedrawEnvelopes();
    }

    private void Vm_OnCycleResetRequested()
    {
        _liveLine.Points.Clear();
        RedrawEnvelopes();
    }

    private void Vm_OnBatchDataReceived(IReadOnlyList<PressPoint> batch)
    {
        double canvasW = CurveCanvas.ActualWidth;
        double canvasH = CurveCanvas.ActualHeight;
        if (canvasW <= 10 || canvasH <= 10) return;

        double maxDisp = _vm.SelectedRecipe?.TargetDisplacement * 1.15 ?? 35.0;
        double maxForce = (_vm.SelectedRecipe?.MaxForceLimit ?? 20.0) * 1.35;

        foreach (var pt in batch)
        {
            double x = (pt.Displacement / maxDisp) * canvasW;
            double y = canvasH - (pt.Force / maxForce) * canvasH;
            _liveLine.Points.Add(new Point(x, y));
        }
    }

    private void RedrawEnvelopes()
    {
        _upperEnvelopeLine.Points.Clear();
        _lowerEnvelopeLine.Points.Clear();

        if (_vm.SelectedRecipe == null) return;

        double canvasW = CurveCanvas.ActualWidth;
        double canvasH = CurveCanvas.ActualHeight;
        if (canvasW <= 10 || canvasH <= 10) return;

        var recipe = _vm.SelectedRecipe;
        double maxDisp = recipe.TargetDisplacement * 1.15;
        double maxForce = recipe.MaxForceLimit * 1.35;
        double marginFactor = recipe.EnvelopeMarginPercentage / 100.0;

        int steps = 100;
        for (int i = 0; i <= steps; i++)
        {
            double disp = (recipe.TargetDisplacement / steps) * i;
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
}
