namespace DiagnosticLabs.Wpf.ViewModels;

/// <summary>A screen with a form that can have changes that are not saved. The main window asks before the user leaves it.</summary>
public interface IDirtyForm
{
    /// <summary>Whether the form differs from what was loaded or saved last.</summary>
    bool IsDirty { get; }

    /// <summary>
    /// Asks what to do with unsaved changes (save, leave them out, or stay). <c>true</c> means the user may leave: nothing was changed,
    /// the changes were saved, or they chose to leave them out.
    /// </summary>
    Task<bool> ConfirmLeaveAsync();
}
