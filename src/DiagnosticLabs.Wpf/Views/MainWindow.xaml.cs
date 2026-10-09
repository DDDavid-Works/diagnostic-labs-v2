using System.Windows;
using DiagnosticLabs.Wpf.ViewModels;

namespace DiagnosticLabs.Wpf.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private bool _signingOut;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
    }

    public event EventHandler? SignedOut;

    private async void OnLoaded(object sender, RoutedEventArgs e) =>
        await _viewModel.LoadMenuCommand.ExecuteAsync(null);

    private void SignOut_OnClick(object sender, RoutedEventArgs e)
    {
        _signingOut = true;
        _viewModel.SignOut();
        SignedOut?.Invoke(this, EventArgs.Empty);
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        if (!_signingOut)
            _viewModel.SignOut();
    }
}
