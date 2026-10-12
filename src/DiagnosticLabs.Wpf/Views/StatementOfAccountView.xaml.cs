using System.Windows;
using System.Windows.Controls;
using DiagnosticLabs.Application.Reports;
using DiagnosticLabs.Wpf.ViewModels;

namespace DiagnosticLabs.Wpf.Views;

public partial class StatementOfAccountView : UserControl
{
    private bool _loaded;

    public StatementOfAccountView() => InitializeComponent();

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Loaded fires again if the control is re-parented; only load once.
        if (_loaded || DataContext is not StatementOfAccountViewModel viewModel)
            return;

        _loaded = true;
        await viewModel.InitializeCommand.ExecuteAsync(null);
    }

    /// <summary>Choosing a patient from the list under the box fills the box and closes the list.</summary>
    private void OnSuggestionPicked(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ListBox { SelectedItem: StatementPatientSuggestion patient } list && DataContext is StatementOfAccountViewModel viewModel)
        {
            viewModel.PickPatientCommand.Execute(patient);
            list.SelectedItem = null;
        }
    }
}
