using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DiagnosticLabs.Wpf.ViewModels;

namespace DiagnosticLabs.Wpf.Controls;

/// <summary>
/// The shared maintenance-screen shell: search box, paged list, edit form with New/Save/Delete.
/// A screen supplies only its list columns (<see cref="GridContent"/>) and its fields (<see cref="FormContent"/>);
/// everything binds to a <see cref="CrudViewModel{TService, TListItem, TDetails, TInput}"/>.
/// </summary>
public partial class CrudLayout : UserControl
{
    public static readonly DependencyProperty GridContentProperty =
        DependencyProperty.Register(nameof(GridContent), typeof(object), typeof(CrudLayout));

    public static readonly DependencyProperty FormContentProperty =
        DependencyProperty.Register(nameof(FormContent), typeof(object), typeof(CrudLayout));

    private bool _loaded;

    public CrudLayout() => InitializeComponent();

    public object? GridContent
    {
        get => GetValue(GridContentProperty);
        set => SetValue(GridContentProperty, value);
    }

    public object? FormContent
    {
        get => GetValue(FormContentProperty);
        set => SetValue(FormContentProperty, value);
    }

    private ICrudScreen? Screen => DataContext as ICrudScreen;

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Loaded fires again if the control is re-parented; only initialise once.
        if (_loaded || Screen is null)
            return;

        _loaded = true;
        await Screen.InitializeCommand.ExecuteAsync(null);
        SearchBox.Focus();
    }

    private void SearchBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            Screen?.SearchCommand.Execute(null);
    }

    private void PageSize_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Ignore the initial binding; only react when the user picks a different size.
        if (IsLoaded && e.RemovedItems.Count > 0)
            Screen?.ChangePageSizeCommand.Execute(null);
    }

    private void IncludeInactive_OnChanged(object sender, RoutedEventArgs e)
    {
        if (IsLoaded)
            Screen?.SearchCommand.Execute(null);
    }

    // Click runs before the Save command, so a cell still being edited in a line grid is committed first.
    private void Save_OnClick(object sender, RoutedEventArgs e)
    {
        foreach (var grid in FindGrids(FormHost))
        {
            grid.CommitEdit(DataGridEditingUnit.Cell, true);
            grid.CommitEdit(DataGridEditingUnit.Row, true);
        }
    }

    private static IEnumerable<DataGrid> FindGrids(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is DataGrid grid)
                yield return grid;

            foreach (var nested in FindGrids(child))
                yield return nested;
        }
    }
}
