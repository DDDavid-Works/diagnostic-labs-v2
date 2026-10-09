using System.Windows;
using System.Windows.Controls;
using DiagnosticLabs.Wpf.ViewModels;

namespace DiagnosticLabs.Wpf.Views;

/// <summary>The one dialog that maintains every dropdown / template list (see <see cref="EntryBuilderViewModel"/>).</summary>
public partial class EntryBuilderWindow : Window
{
    public EntryBuilderWindow(EntryBuilderViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.CloseRequested += (_, saved) =>
        {
            Saved = saved;
            Close();
        };
    }

    /// <summary>True when the user saved the list; false when they cancelled or closed the window.</summary>
    public bool Saved { get; private set; }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is EntryBuilderViewModel viewModel)
            await viewModel.InitializeCommand.ExecuteAsync(null);
    }

    // A row that was just added puts the cursor in its box so the user can type straight away.
    private void RowTextBox_OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox { DataContext: SingleLineRowViewModel { FocusRequested: true } row } box)
        {
            row.FocusRequested = false;
            box.Focus();
            box.BringIntoView();
        }
    }
}
