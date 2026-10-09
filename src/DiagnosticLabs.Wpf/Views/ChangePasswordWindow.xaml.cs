using System.Windows;
using DiagnosticLabs.Wpf.ViewModels;

namespace DiagnosticLabs.Wpf.Views;

public partial class ChangePasswordWindow : Window
{
    private readonly ChangePasswordViewModel _viewModel;

    public ChangePasswordWindow(ChangePasswordViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        viewModel.CloseRequested += (_, changed) =>
        {
            Changed = changed;
            Close();
        };
        Loaded += (_, _) => OldBox.Focus();
    }

    /// <summary>True when the password was changed; false when the user cancelled or closed the window.</summary>
    public bool Changed { get; private set; }

    // PasswordBox.Password is intentionally not bindable; forward it to the view model here.
    private void OldBox_OnPasswordChanged(object sender, RoutedEventArgs e) => _viewModel.OldPassword = OldBox.Password;

    private void NewBox_OnPasswordChanged(object sender, RoutedEventArgs e) => _viewModel.NewPassword = NewBox.Password;

    private void ConfirmBox_OnPasswordChanged(object sender, RoutedEventArgs e) => _viewModel.ConfirmPassword = ConfirmBox.Password;
}
