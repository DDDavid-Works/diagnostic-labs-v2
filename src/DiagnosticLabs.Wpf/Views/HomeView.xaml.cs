using System.Windows;
using System.Windows.Controls;
using DiagnosticLabs.Wpf.ViewModels;

namespace DiagnosticLabs.Wpf.Views;

public partial class HomeView : UserControl
{
    private bool _loaded;

    public HomeView() => InitializeComponent();

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Loaded fires again if the control is re-parented; only load once.
        if (_loaded || DataContext is not HomeViewModel viewModel)
            return;

        _loaded = true;
        await viewModel.InitializeCommand.ExecuteAsync(null);
    }
}
