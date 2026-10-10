using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DiagnosticLabs.Wpf.ViewModels;

namespace DiagnosticLabs.Wpf.Controls;

/// <summary>A multi-line text box with a quiet "Saved texts" dropdown in its upper right corner.</summary>
public partial class TemplateTextBox : UserControl
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(TemplateTextBox),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty OptionsProperty = DependencyProperty.Register(
        nameof(Options), typeof(TemplateOptions), typeof(TemplateTextBox));

    public static readonly DependencyProperty EditCommandProperty = DependencyProperty.Register(
        nameof(EditCommand), typeof(ICommand), typeof(TemplateTextBox));

    /// <summary>For a narrow box: the dropdown takes its own line at the top of the box instead of the right end of the first line.</summary>
    public static readonly DependencyProperty PickerOnTopProperty = DependencyProperty.Register(
        nameof(PickerOnTop), typeof(bool), typeof(TemplateTextBox),
        new PropertyMetadata(false, (d, e) => ((TemplateTextBox)d).Box.Padding = (bool)e.NewValue ? new Thickness(6, 24, 6, 5) : new Thickness(6, 5, 118, 5)));

    public TemplateTextBox() => InitializeComponent();

    public bool PickerOnTop
    {
        get => (bool)GetValue(PickerOnTopProperty);
        set => SetValue(PickerOnTopProperty, value);
    }

    public string? Text
    {
        get => (string?)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
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
}
