using System.Windows;

namespace DiagnosticLabs.Wpf.Services;

public interface IDialogService
{
    bool Confirm(string message, string title);

    void Inform(string message, string title);
}

public sealed class DialogService : IDialogService
{
    public bool Confirm(string message, string title) =>
        MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;

    public void Inform(string message, string title) =>
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
}
