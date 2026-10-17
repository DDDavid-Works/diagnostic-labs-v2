using System.Windows;
using System.Windows.Controls;
using DiagnosticLabs.Wpf.ViewModels;

namespace DiagnosticLabs.Wpf.Views;

public partial class StationsView : UserControl
{
    private bool _loaded;

    public StationsView() => InitializeComponent();

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Loaded fires again if the control is re-parented; only load once.
        if (_loaded || DataContext is not StationsViewModel viewModel)
            return;

        _loaded = true;
        await viewModel.RefreshCommand.ExecuteAsync(null);
    }
}
