using System.Windows;
using System.Windows.Controls;

namespace DiagnosticLabs.Wpf.Views;

public partial class FormChoiceWindow : Window
{
    public FormChoiceWindow(string message, string title, IReadOnlyList<string> options, int suggested)
    {
        InitializeComponent();
        Title = title;
        MessageText.Text = message;

        for (var i = 0; i < options.Count; i++)
        {
            var index = i;
            var button = new Button
            {
                Content = options[i],
                Height = 38,
                Margin = new Thickness(0, 0, 0, 8),
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(14, 0, 14, 0),
                FontSize = 14,
                IsDefault = i == suggested,
            };
            if (i == suggested)
                button.Style = (Style)FindResource("PrimaryButton");

            button.Click += (_, _) =>
            {
                Chosen = index;
                Close();
            };
            OptionPanel.Children.Add(button);
        }
    }

    /// <summary>The index of the option pressed; null when the user cancelled or closed the window.</summary>
    public int? Chosen { get; private set; }
}
