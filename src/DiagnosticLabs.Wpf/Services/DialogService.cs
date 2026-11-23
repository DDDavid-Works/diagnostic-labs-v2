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

    /// <summary>
    /// One pill per option, drawn like the home chips (ticked when it already has a record; the <paramref name="suggested"/> one is the default
    /// button) and Cancel; the index chosen, or null on Cancel.
    /// </summary>
    int? Choose(string message, string title, IReadOnlyList<ChoiceOption> options, int suggested);
}

/// <summary>An answer of <see cref="IDialogService.Choose"/>: its caption, whether it already has a record (ticked), and what clicking it does.</summary>
public sealed record ChoiceOption(string Caption, bool IsMade, string? ToolTip = null);

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

    public int? Choose(string message, string title, IReadOnlyList<ChoiceOption> options, int suggested)
    {
        var window = new Views.FormChoiceWindow(message, title, options, suggested)
        {
            Owner = System.Windows.Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                    ?? System.Windows.Application.Current.MainWindow,
        };

        window.ShowDialog();
        return window.Chosen;
    }
}
