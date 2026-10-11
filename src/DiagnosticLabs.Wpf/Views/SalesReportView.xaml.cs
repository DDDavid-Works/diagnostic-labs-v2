using System.Windows;
using System.Windows.Controls;
using DiagnosticLabs.Wpf.ViewModels;

namespace DiagnosticLabs.Wpf.Views;

public partial class SalesReportView : UserControl
{
    private bool _loaded;

    public SalesReportView() => InitializeComponent();

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Loaded fires again if the control is re-parented; only load once.
        if (_loaded || DataContext is not SalesReportViewModel viewModel)
            return;

        _loaded = true;
        await viewModel.InitializeCommand.ExecuteAsync(null);
    }
}
