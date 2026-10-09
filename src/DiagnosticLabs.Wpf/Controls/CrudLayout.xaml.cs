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

    public static readonly DependencyProperty FooterContentProperty =
        DependencyProperty.Register(nameof(FooterContent), typeof(object), typeof(CrudLayout));

    public static readonly DependencyProperty SearchFiltersProperty =
        DependencyProperty.Register(nameof(SearchFilters), typeof(object), typeof(CrudLayout));

    public static readonly DependencyProperty FormMaxWidthProperty =
        DependencyProperty.Register(nameof(FormMaxWidth), typeof(double), typeof(CrudLayout), new PropertyMetadata(680d));

    private bool _loaded;

    public CrudLayout() => InitializeComponent();

    /// <summary>Extra buttons next to New / Save / Delete.</summary>
    public object? FooterContent
    {
        get => GetValue(FooterContentProperty);
        set => SetValue(FooterContentProperty, value);
    }

    /// <summary>Extra filters shown above the list (e.g. company and date on the registration screen).</summary>
    public object? SearchFilters
    {
        get => GetValue(SearchFiltersProperty);
        set => SetValue(SearchFiltersProperty, value);
    }

    /// <summary>Widest the form may grow; wide, wrapping forms raise it.</summary>
    public double FormMaxWidth
    {
        get => (double)GetValue(FormMaxWidthProperty);
        set => SetValue(FormMaxWidthProperty, value);
    }

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
        Screen.NewRecordStarted += (_, _) => FocusFirstField();
        await Screen.InitializeCommand.ExecuteAsync(null);
        FocusFirstField();
    }

    // Ctrl+S saves from anywhere on the screen; like the Save button, it first commits a cell still being edited.
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control && Screen?.SaveCommand is { } save)
        {
            CommitGridEdits();
            (Keyboard.FocusedElement as TextBox)?.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            if (save.CanExecute(null))
                save.Execute(null);

            e.Handled = true;
        }
    }

    private void SearchPanel_OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        // Opening the list puts the cursor in the search box, ready to type.
        if (e.NewValue is true)
            Dispatcher.BeginInvoke(() => SearchBox.Focus(), System.Windows.Threading.DispatcherPriority.Input);
    }

    // A field can claim the cursor with Tag="Autofocus"; otherwise the first editable text box gets it.
    private void FocusFirstField() =>
        Dispatcher.BeginInvoke(
            () => (FindFirst<FrameworkElement>(FormHost, e => e is TextBox && e.IsEnabled && Equals(e.Tag, "Autofocus"))
                   ?? FindFirst<TextBox>(FormHost, t => t.IsEnabled && !t.IsReadOnly))?.Focus(),
            System.Windows.Threading.DispatcherPriority.Input);

    private static T? FindFirst<T>(DependencyObject root, Func<T, bool> where) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match && where(match))
                return match;

            if (FindFirst(child, where) is { } nested)
                return nested;
        }

        return null;
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
    private void Save_OnClick(object sender, RoutedEventArgs e) => CommitGridEdits();

    private void CommitGridEdits()
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
