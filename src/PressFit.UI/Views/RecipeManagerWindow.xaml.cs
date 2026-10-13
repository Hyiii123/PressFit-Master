using System.Windows;
using System.Windows.Controls;
using PressFit.Communication.Modbus;
using PressFit.Core.Interfaces;
using PressFit.Core.Models;

namespace PressFit.UI.Views;

public partial class RecipeManagerWindow : Window
{
    private readonly IPressFitRepository _repo;
    private readonly IModbusMasterService _modbus;
    private PressRecipe? _currentRecipe;

    public event Action? OnRecipesChanged;

    public RecipeManagerWindow(IPressFitRepository repo, IModbusMasterService modbus)
    {
        InitializeComponent();
        _repo = repo;
        _modbus = modbus;
        Loaded += async (s, e) => await RefreshListAsync();
    }

    private async Task RefreshListAsync()
    {
        var list = await _repo.GetAllRecipesAsync();
        RecipeGrid.ItemsSource = list;
        if (list.Count > 0 && _currentRecipe == null)
        {
            RecipeGrid.SelectedIndex = 0;
        }
    }

    private void RecipeGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RecipeGrid.SelectedItem is PressRecipe r)
        {
            _currentRecipe = r;
            TxtCode.Text = r.RecipeCode;
            TxtName.Text = r.ProductName;
            TxtTargetDisp.Text = r.TargetDisplacement.ToString("F2");
            TxtDispTol.Text = r.DisplacementTolerance.ToString("F2");
            TxtMaxForce.Text = r.MaxForceLimit.ToString("F2");
            TxtMinForce.Text = r.MinForceLimit.ToString("F2");
            TxtApproachSpeed.Text = r.ApproachSpeed.ToString("F1");
            TxtPressSpeed.Text = r.PressSpeed.ToString("F1");
            TxtHoldingTime.Text = r.HoldingTimeSec.ToString("F1");
            TxtEnvelopeMargin.Text = r.EnvelopeMarginPercentage.ToString("F1");
        }
    }

    private void NewRecipe_Click(object sender, RoutedEventArgs e)
    {
        _currentRecipe = new PressRecipe();
        TxtCode.Text = $"REC-NEW-{DateTime.Now:fff}";
        TxtName.Text = "新产品精密装配";
        TxtTargetDisp.Text = "20.00";
        TxtDispTol.Text = "0.10";
        TxtMaxForce.Text = "15.00";
        TxtMinForce.Text = "8.00";
        TxtApproachSpeed.Text = "40.0";
        TxtPressSpeed.Text = "4.0";
        TxtHoldingTime.Text = "1.0";
        TxtEnvelopeMargin.Text = "10.0";
    }

    private async void SaveRecipe_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtCode.Text))
        {
            MessageBox.Show("配方编号不能为空", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var recipe = _currentRecipe ?? new PressRecipe();
            recipe.RecipeCode = TxtCode.Text.Trim();
            recipe.ProductName = TxtName.Text.Trim();
            recipe.TargetDisplacement = double.Parse(TxtTargetDisp.Text);
            recipe.DisplacementTolerance = double.Parse(TxtDispTol.Text);
            recipe.MaxForceLimit = double.Parse(TxtMaxForce.Text);
            recipe.MinForceLimit = double.Parse(TxtMinForce.Text);
            recipe.ApproachSpeed = double.Parse(TxtApproachSpeed.Text);
            recipe.PressSpeed = double.Parse(TxtPressSpeed.Text);
            recipe.HoldingTimeSec = double.Parse(TxtHoldingTime.Text);
            recipe.EnvelopeMarginPercentage = double.Parse(TxtEnvelopeMargin.Text);

            if (recipe.MaxForceLimit <= recipe.MinForceLimit)
            {
                MessageBox.Show("压力上限必须大于压力下限！", "参数校验失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            await _repo.SaveRecipeAsync(recipe);
            MessageBox.Show("配方保存成功！", "通知", MessageBoxButton.OK, MessageBoxImage.Information);
            await RefreshListAsync();
            OnRecipesChanged?.Invoke();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"保存失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void DeleteRecipe_Click(object sender, RoutedEventArgs e)
    {
        if (_currentRecipe == null || _currentRecipe.Id == 0) return;

        if (MessageBox.Show($"确定要删除配方 [{_currentRecipe.RecipeCode}] 吗？", "二次确认", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            await _repo.DeleteRecipeAsync(_currentRecipe.Id);
            _currentRecipe = null;
            await RefreshListAsync();
            OnRecipesChanged?.Invoke();
        }
    }

    private async void DownloadToPlc_Click(object sender, RoutedEventArgs e)
    {
        if (_currentRecipe == null)
        {
            MessageBox.Show("请先选择要下发的配方！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!_modbus.IsConnected)
        {
            MessageBox.Show("下位机通信离线，无法下发参数！", "通信异常", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        try
        {
            // 通过 Modbus 0x10 功能码批量下发参数寄存器 10..12
            short[] values = new short[3];
            values[0] = (short)(_currentRecipe.TargetDisplacement * 100);
            values[1] = (short)(_currentRecipe.MaxForceLimit * 100);
            values[2] = (short)(_currentRecipe.MinForceLimit * 100);

            await _modbus.WriteMultipleRegistersAsync(1, ModbusAddressMap.REG_RECIPE_TARGET_DISP, values);
            MessageBox.Show($"配方 [{_currentRecipe.RecipeCode}] 参数已成功下发至下位机保持寄存器 10..12！", "下发成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"下发异常: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
