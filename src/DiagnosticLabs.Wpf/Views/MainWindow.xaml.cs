using System.ComponentModel;
using System.Windows;
using DiagnosticLabs.Wpf.ViewModels;

namespace DiagnosticLabs.Wpf.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private bool _signingOut;
    private bool _leaveConfirmed;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
    }

    public event EventHandler? SignedOut;

    private async void OnLoaded(object sender, RoutedEventArgs e) =>
        await _viewModel.LoadMenuCommand.ExecuteAsync(null);

    private async void SignOut_OnClick(object sender, RoutedEventArgs e)
    {
        // A form with changes that are not saved asks first.
        if (!await _viewModel.CanLeaveAsync())
            return;

        _leaveConfirmed = true;
        _signingOut = true;
        _viewModel.SignOut();
        SignedOut?.Invoke(this, EventArgs.Empty);
        Close();
    }

    protected override async void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        if (_leaveConfirmed)
            return;

        // Closing the window is decided after the question has been answered, so hold it back for now.
        e.Cancel = true;

        // Let this closing request finish first: with nothing to ask the answer is ready at once, and a window cannot be closed again from inside its own closing.
        await System.Windows.Threading.Dispatcher.Yield();
        if (await _viewModel.CanLeaveAsync())
        {
            _leaveConfirmed = true;
            Close();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        if (!_signingOut)
            _viewModel.SignOut();
    }
}
