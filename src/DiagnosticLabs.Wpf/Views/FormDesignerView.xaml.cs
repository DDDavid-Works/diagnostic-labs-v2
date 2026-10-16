using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DiagnosticLabs.Wpf.ViewModels;

namespace DiagnosticLabs.Wpf.Views;

public partial class FormDesignerView : UserControl
{
    private bool _loaded;
    private Point _dragStart;
    private RowViewModel? _dragRow;

    public FormDesignerView() => InitializeComponent();

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Loaded fires again if the control is re-parented; only load once.
        if (_loaded || DataContext is not FormDesignerViewModel viewModel)
            return;

        _loaded = true;
        await viewModel.InitializeCommand.ExecuteAsync(null);
    }

    // ---------------------------------------------------------------- dragging a row (by its grip) to a new place in its group

    private bool CanEditForm => DataContext is FormDesignerViewModel { CanEdit: true };

    private void OnGripMouseDown(object sender, MouseButtonEventArgs e)
    {
        _dragRow = sender is FrameworkElement { DataContext: RowViewModel row } && CanEditForm ? row : null;
        _dragStart = e.GetPosition(null);
    }

    private void OnGripMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _dragRow is null)
            return;

        var position = e.GetPosition(null);
        if (Math.Abs(position.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(position.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        var row = _dragRow;
        _dragRow = null;
        DragDrop.DoDragDrop((DependencyObject)sender, new DataObject(typeof(RowViewModel), row), DragDropEffects.Move);
    }

    /// <summary>The row being dragged, when the row under the mouse is in the same group (rows do not move between groups).</summary>
    private static RowViewModel? DraggedOver(object sender, DragEventArgs e) =>
        sender is FrameworkElement { DataContext: RowViewModel target }
        && e.Data.GetData(typeof(RowViewModel)) is RowViewModel source
        && source.Owner is not null && ReferenceEquals(source.Owner, target.Owner)
            ? source
            : null;

    private void OnRowDragEnter(object sender, DragEventArgs e)
    {
        e.Effects = DraggedOver(sender, e) is null ? DragDropEffects.None : DragDropEffects.Move;
        if (e.Effects == DragDropEffects.Move && sender is Border border)
            border.BorderBrush = (Brush)FindResource("AccentBrush");

        e.Handled = true;
    }

    private void OnRowDragOver(object sender, DragEventArgs e)
    {
        e.Effects = DraggedOver(sender, e) is null ? DragDropEffects.None : DragDropEffects.Move;
        e.Handled = true;
    }

    private void OnRowDragLeave(object sender, DragEventArgs e)
    {
        if (sender is Border border)
            border.BorderBrush = Brushes.Transparent;
    }

    private void OnRowDrop(object sender, DragEventArgs e)
    {
        if (sender is Border border)
            border.BorderBrush = Brushes.Transparent;

        if (DraggedOver(sender, e) is { } source && sender is FrameworkElement { DataContext: RowViewModel target })
            source.Owner!.MoveRow(source, target);

        e.Handled = true;
    }
}
