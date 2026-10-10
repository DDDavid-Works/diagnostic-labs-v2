using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DiagnosticLabs.Wpf.Controls;

/// <summary>
/// A dropdown of maintained choices that can also be typed into. A value typed here is kept on the record but never added to the list.
/// When the user may edit the list, its last row is "Edit entries...", which opens the Entry Builder.
/// </summary>
public partial class ChoiceBox : UserControl
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(ChoiceBox),
        new FrameworkPropertyMetadata(
            null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, e) => ((ChoiceBox)d).ShowText((string?)e.NewValue)));

    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource), typeof(IEnumerable), typeof(ChoiceBox), new PropertyMetadata(null, (d, e) => ((ChoiceBox)d).Watch(e.NewValue as IEnumerable)));

    public static readonly DependencyProperty EditCommandProperty = DependencyProperty.Register(
        nameof(EditCommand), typeof(ICommand), typeof(ChoiceBox), new PropertyMetadata(null, (d, e) => ((ChoiceBox)d).WatchCommand(e.OldValue as ICommand, e.NewValue as ICommand)));

    private static readonly EditEntriesItem EditRow = new("Edit entries...");

    private readonly ObservableCollection<object> _entries = [];
    private INotifyCollectionChanged? _watched;
    private string? _textBeforePicking;
    private bool _rebuilding;

    public ChoiceBox()
    {
        InitializeComponent();
        Combo.ItemsSource = _entries;
        Combo.AddHandler(System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent, new TextChangedEventHandler(OnComboTextChanged));
        Loaded += (_, _) => Rebuild();
    }

    public string? Text
    {
        get => (string?)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public ICommand? EditCommand
    {
        get => (ICommand?)GetValue(EditCommandProperty);
        set => SetValue(EditCommandProperty, value);
    }

    private void Watch(IEnumerable? source)
    {
        if (_watched is not null)
            _watched.CollectionChanged -= OnSourceChanged;

        _watched = source as INotifyCollectionChanged;
        if (_watched is not null)
            _watched.CollectionChanged += OnSourceChanged;

        Rebuild();
    }

    private void OnSourceChanged(object? sender, NotifyCollectionChangedEventArgs e) => Rebuild();
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

    /// <summary>The dropdown shows the choices, then "Edit entries..." when the user is allowed to edit the list.</summary>
    private void Rebuild()
    {
        // Swapping the items can blank an editable combo, so put the text back afterwards (without telling the form it was ever blank).
        var text = Text;
        _rebuilding = true;
        try
        {
            _entries.Clear();
            if (ItemsSource is not null)
            {
                foreach (var item in ItemsSource)
                    _entries.Add(item);
            }

            if (EditCommand?.CanExecute(null) == true)
                _entries.Add(EditRow);

            Combo.Text = text ?? string.Empty;
        }
        finally
        {
            _rebuilding = false;
        }
    }

    /// <summary>A value set from the form (a loaded record, a default) is shown in the box.</summary>
    private void ShowText(string? value)
    {
        if (!_rebuilding && Combo.Text != (value ?? string.Empty))
            Combo.Text = value ?? string.Empty;
    }

    /// <summary>Typing or picking a value reaches the form straight away, not when the box loses focus.</summary>
    private void OnComboTextChanged(object sender, TextChangedEventArgs e)
    {
        // Choosing the "Edit entries..." row shows empty text for a moment; that is not the user clearing the field.
        var choosingEditRow = Combo.SelectedItem is EditEntriesItem || (Combo.IsDropDownOpen && Combo.Text.Length == 0 && Text is { Length: > 0 });
        if (_rebuilding || choosingEditRow)
            return;

        var text = Combo.Text.Length == 0 ? null : Combo.Text;
        if (Text != text)
            Text = text;
    }

    private void OnDropDownOpened(object? sender, EventArgs e) => _textBeforePicking = Combo.Text;

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Combo.SelectedItem is not EditEntriesItem)
            return;

        // Choosing the row must not change the field: put the old text back, then open the builder.
        var previous = _textBeforePicking;
        Dispatcher.BeginInvoke(() =>
        {
            Combo.SelectedItem = null;
            Combo.Text = previous ?? string.Empty;
            if (EditCommand?.CanExecute(null) == true)
                EditCommand.Execute(null);
        });
    }
}