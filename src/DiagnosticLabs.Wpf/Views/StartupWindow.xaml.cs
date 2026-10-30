using System.Windows;

namespace DiagnosticLabs.Wpf.Views;

public partial class StartupWindow : Window
{
    public StartupWindow() => InitializeComponent();

    public string Status
    {
        set => StatusText.Text = value;
    }
}
