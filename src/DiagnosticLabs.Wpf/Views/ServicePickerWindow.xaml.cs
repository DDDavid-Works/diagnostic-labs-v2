using System.Windows;
using DiagnosticLabs.Wpf.ViewModels;

namespace DiagnosticLabs.Wpf.Views;

public partial class ServicePickerWindow : Window
{
    public ServicePickerWindow(ServicePickerViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.CloseRequested += (_, accepted) =>
        {
            Accepted = accepted;
            Close();
        };
    }

    /// <summary>True when the user pressed Ok; false when they cancelled or closed the window.</summary>
    public bool Accepted { get; private set; }
}
