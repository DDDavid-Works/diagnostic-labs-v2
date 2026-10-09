using System.Windows;
using DiagnosticLabs.Wpf.ViewModels;

namespace DiagnosticLabs.Wpf.Views;

public partial class LoginWindow : Window
{
    private readonly LoginViewModel _viewModel;

    public LoginWindow(LoginViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        viewModel.LoginSucceeded += (_, _) =>
        {
            LoginSucceeded?.Invoke(this, EventArgs.Empty);
            Close();
        };
        Loaded += async (_, _) =>
        {
            UsernameBox.Focus();
            await viewModel.LoadBrandingAsync();
        };
    }

    public event EventHandler? LoginSucceeded;

    // PasswordBox.Password is intentionally not bindable; forward it to the view model here.
    private void PasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e) =>
        _viewModel.Password = PasswordBox.Password;
}
