using System.Windows;
using System.Windows.Controls;
using PressFit.Core.Enums;

namespace PressFit.UI.Views;

public partial class UserLoginDialog : Window
{
    public UserRole SelectedRole { get; private set; } = UserRole.Operator;

    public UserLoginDialog()
    {
        InitializeComponent();
    }

    private void RoleComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PasswordLabel == null || PasswordInput == null) return;

        var selected = (RoleComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        bool needPassword = selected != "Operator";
        PasswordLabel.Visibility = needPassword ? Visibility.Visible : Visibility.Collapsed;
        PasswordInput.Visibility = needPassword ? Visibility.Visible : Visibility.Collapsed;
        ErrorMsg.Visibility = Visibility.Collapsed;
        PasswordInput.Password = string.Empty;
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        var tag = (RoleComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        if (tag == "Engineer")
        {
            if (PasswordInput.Password != "1234")
            {
                ShowError("工程师密码错误 (默认: 1234)");
                return;
            }
            SelectedRole = UserRole.Engineer;
        }
        else if (tag == "Administrator")
        {
            if (PasswordInput.Password != "8888")
            {
                ShowError("管理员密码错误 (默认: 8888)");
                return;
            }
            SelectedRole = UserRole.Administrator;
        }
        else
        {
            SelectedRole = UserRole.Operator;
        }

        DialogResult = true;
        Close();
    }

    private void ShowError(string msg)
    {
        ErrorMsg.Text = msg;
        ErrorMsg.Visibility = Visibility.Visible;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
