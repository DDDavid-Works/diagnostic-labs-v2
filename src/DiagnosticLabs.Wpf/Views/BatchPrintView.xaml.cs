using System.Windows;
using System.Windows.Controls;
using DiagnosticLabs.Wpf.ViewModels;

namespace DiagnosticLabs.Wpf.Views;

public partial class BatchPrintView : UserControl
{
    private bool _loaded;

    public BatchPrintView() => InitializeComponent();

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Loaded fires again if the control is re-parented; only load once.
        if (_loaded || DataContext is not BatchPrintViewModel viewModel)
            return;

        _loaded = true;
        await viewModel.InitializeCommand.ExecuteAsync(null);
    }
}
