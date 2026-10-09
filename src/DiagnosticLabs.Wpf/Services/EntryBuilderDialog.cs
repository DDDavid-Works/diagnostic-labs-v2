using System.Windows;
using DiagnosticLabs.Application.Entries;
using DiagnosticLabs.Wpf.ViewModels;
using DiagnosticLabs.Wpf.Views;
using Microsoft.Extensions.DependencyInjection;

namespace DiagnosticLabs.Wpf.Services;

/// <summary>Opens the shared Entry Builder for a list; the result says whether the list was saved (so the caller refreshes its dropdown).</summary>
public interface IEntryBuilderDialog
{
    /// <param name="field">The list to edit.</param>
    /// <param name="hostModuleId">The screen the field is on; the user needs that screen's Edit permission.</param>
    bool EditSingleLine(EntryField field, int hostModuleId);

    bool EditMultiLine(EntryField field, int hostModuleId);
}

public sealed class EntryBuilderDialog(IServiceProvider services) : IEntryBuilderDialog
{
    public bool EditSingleLine(EntryField field, int hostModuleId) =>
        Show(ActivatorUtilities.CreateInstance<SingleLineEntryBuilderViewModel>(services, field, hostModuleId), width: 420, height: 520);

    public bool EditMultiLine(EntryField field, int hostModuleId) =>
        Show(ActivatorUtilities.CreateInstance<MultiLineEntryBuilderViewModel>(services, field, hostModuleId), width: 760, height: 520);

    private static bool Show(EntryBuilderViewModel viewModel, double width, double height)
    {
        var window = new EntryBuilderWindow(viewModel)
        {
            Owner = System.Windows.Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive) ?? System.Windows.Application.Current.MainWindow,
            Width = width,
            Height = height,
        };

        window.ShowDialog();
        return window.Saved;
    }
}
