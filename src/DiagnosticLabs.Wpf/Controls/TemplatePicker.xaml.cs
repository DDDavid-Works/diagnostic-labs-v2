using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DiagnosticLabs.Application.Entries;
using DiagnosticLabs.Wpf.ViewModels;

namespace DiagnosticLabs.Wpf.Controls;

/// <summary>A dropdown of saved texts; its last row, "Edit saved texts...", opens the Entry Builder for the list.</summary>
public partial class TemplatePicker : UserControl
{
    public static readonly DependencyProperty OptionsProperty = DependencyProperty.Register(
        nameof(Options), typeof(TemplateOptions), typeof(TemplatePicker), new PropertyMetadata(null, (d, e) => ((TemplatePicker)d).Watch(e.NewValue as TemplateOptions)));

    public static readonly DependencyProperty EditCommandProperty = DependencyProperty.Register(
        nameof(EditCommand), typeof(ICommand), typeof(TemplatePicker), new PropertyMetadata(null, (d, e) => ((TemplatePicker)d).WatchCommand(e.OldValue as ICommand, e.NewValue as ICommand)));

    private static readonly EditEntriesItem EditRow = new("Edit saved texts...");

    private readonly ObservableCollection<object> _entries = [];
    private TemplateOptions? _watched;

    public TemplatePicker()
    {
        InitializeComponent();
        Combo.ItemsSource = _entries;
        Loaded += (_, _) => Rebuild();
    }

    public TemplateOptions? Options
    {
        get => (TemplateOptions?)GetValue(OptionsProperty);
        set => SetValue(OptionsProperty, value);
    }

    public ICommand? EditCommand
    {
        get => (ICommand?)GetValue(EditCommandProperty);
        set => SetValue(EditCommandProperty, value);
    }

    private void Watch(TemplateOptions? options)
    {
        if (_watched is not null)
            ((INotifyCollectionChanged)_watched.Items).CollectionChanged -= OnItemsChanged;

        _watched = options;
        if (_watched is not null)
            ((INotifyCollectionChanged)_watched.Items).CollectionChanged += OnItemsChanged;

        Rebuild();
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e) => Rebuild();
    // The command can switch itself off while it runs (the Entry Builder is open), so the row is re-evaluated whenever that changes.
    private void WatchCommand(ICommand? oldCommand, ICommand? newCommand)
    {
        if (oldCommand is not null)
            oldCommand.CanExecuteChanged -= OnCommandStateChanged;

        if (newCommand is not null)
            newCommand.CanExecuteChanged += OnCommandStateChanged;

        Rebuild();
    }

    private void OnCommandStateChanged(object? sender, EventArgs e) => Rebuild();

    private void Rebuild()
    {
        _entries.Clear();
        if (Options is not null)
        {
            foreach (var entry in Options.Items)
                _entries.Add(entry);
        }

        if (EditCommand?.CanExecute(null) == true)
            _entries.Add(EditRow);
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var picked = Combo.SelectedItem;
        if (picked is null)
            return;

        // Whatever was chosen, the box goes back to showing "Saved texts (n)" so the same text can be picked again.
        Dispatcher.BeginInvoke(() =>
        {
            Combo.SelectedItem = null;
            if (picked is MultiLineEntry entry)
                Options!.Selected = entry;
            else if (picked is EditEntriesItem && EditCommand?.CanExecute(null) == true)
                EditCommand.Execute(null);
        });
    }
}