using System.Windows;

namespace DiagnosticLabs.Wpf.Services;

/// <summary>What the user chose when asked about changes that are not saved.</summary>
public enum SaveChoice
{
    Save,
    Discard,
    Cancel,
}

public interface IDialogService
{
    bool Confirm(string message, string title);

    void Inform(string message, string title);

    /// <summary>"Save", "Don't save" or "Cancel" (stay where you are) for a form that has changes.</summary>
    SaveChoice AskToSave(string message, string title);
}

public sealed class DialogService : IDialogService
{
    public bool Confirm(string message, string title) =>
        MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;

    public void Inform(string message, string title) =>
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);

    public SaveChoice AskToSave(string message, string title) =>
        MessageBox.Show(message + "\n\nYes saves them, No leaves them out, Cancel keeps you here.", title, MessageBoxButton.YesNoCancel, MessageBoxImage.Question) switch
        {
            MessageBoxResult.Yes => SaveChoice.Save,
            MessageBoxResult.No => SaveChoice.Discard,
            _ => SaveChoice.Cancel,
        };
}
