using System.Windows;
using DiagnosticLabs.Application.Lookups;
using DiagnosticLabs.Wpf.ViewModels;
using DiagnosticLabs.Wpf.Views;

namespace DiagnosticLabs.Wpf.Services;

public interface IServicePickerDialog
{
    /// <summary>Shows every service with <paramref name="selectedIds"/> ticked; returns the ticked ids after Ok, or <c>null</c> on Cancel.</summary>
    IReadOnlyList<long>? Pick(IReadOnlyList<ServiceOption> options, IEnumerable<long> selectedIds);
}

public sealed class ServicePickerDialog : IServicePickerDialog
{
    public IReadOnlyList<long>? Pick(IReadOnlyList<ServiceOption> options, IEnumerable<long> selectedIds)
    {
        var viewModel = new ServicePickerViewModel(options, selectedIds);
        var window = new ServicePickerWindow(viewModel)
        {
            Owner = System.Windows.Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                    ?? System.Windows.Application.Current.MainWindow,
        };

        window.ShowDialog();
        return window.Accepted ? viewModel.SelectedIds : null;
    }
}
