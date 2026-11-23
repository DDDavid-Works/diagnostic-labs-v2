using System.Windows;
using System.Windows.Controls;
using DiagnosticLabs.Wpf.Services;

namespace DiagnosticLabs.Wpf.Views;

public partial class FormChoiceWindow : Window
{
    public FormChoiceWindow(string message, string title, IReadOnlyList<ChoiceOption> options, int suggested)
    {
        InitializeComponent();
        Title = title;
        MessageText.Text = message;

        // Each answer is a pill like the home chips (bound to the option's Caption and IsMade); the suggested one is the default button.
        for (var i = 0; i < options.Count; i++)
        {
            var index = i;
            var button = new Button
            {
                Style = (Style)FindResource("ChipButton"),
                DataContext = options[i],
                ToolTip = options[i].ToolTip,
                IsDefault = i == suggested,
            };
            button.Click += (_, _) =>
            {
                Chosen = index;
                Close();
            };
            OptionPanel.Children.Add(button);
            if (i == suggested)
                Loaded += (_, _) => button.Focus();
        }
    }

    /// <summary>The index of the option pressed; null when the user cancelled or closed the window.</summary>
    public int? Chosen { get; private set; }
}
