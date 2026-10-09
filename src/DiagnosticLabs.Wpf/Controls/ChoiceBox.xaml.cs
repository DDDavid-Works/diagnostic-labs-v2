using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DiagnosticLabs.Wpf.Controls;

/// <summary>
/// A dropdown of maintained choices that can also be typed into, with a button that opens the Entry Builder
/// for the list. A typed value is kept on the record but never added to the list.
/// </summary>
public partial class ChoiceBox : UserControl
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(ChoiceBox),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable), typeof(ChoiceBox));

    public static readonly DependencyProperty EditCommandProperty =
        DependencyProperty.Register(nameof(EditCommand), typeof(ICommand), typeof(ChoiceBox));

    public ChoiceBox() => InitializeComponent();

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
}
