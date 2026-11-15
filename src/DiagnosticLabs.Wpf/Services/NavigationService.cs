using DiagnosticLabs.Domain.Catalog;

namespace DiagnosticLabs.Wpf.Services;

/// <summary>Lets one screen send the user to another (e.g. "Pay now" on a registration) without knowing about the main window.</summary>
public interface INavigationService
{
    /// <summary>Set by the main window; opens Payments with this registration already loaded.</summary>
    Action<long>? PaymentHandler { get; set; }

    /// <summary>
    /// Set by the main window; opens a result screen (module) for a registration: the given result when there is one, else a new one. The section
    /// is set when the result goes in one section of the Physical Examination sheet.
    /// </summary>
    Action<int, long, long?, FormSection?>? ResultHandler { get; set; }

    /// <summary>Set by the main window; reloads the menu (a form was made, switched off or on in the Custom Lab Form designer) without leaving the screen on show.</summary>
    Action? MenuChangedHandler { get; set; }

    void OpenPayment(long registrationId);

    void OpenResult(int moduleId, long registrationId, long? resultId, FormSection? section = null);

    void MenuChanged();
}

public sealed class NavigationService : INavigationService
{
    public Action<long>? PaymentHandler { get; set; }

    public Action<int, long, long?, FormSection?>? ResultHandler { get; set; }

    public Action? MenuChangedHandler { get; set; }

    public void OpenPayment(long registrationId) => PaymentHandler?.Invoke(registrationId);

    public void OpenResult(int moduleId, long registrationId, long? resultId, FormSection? section = null) =>
        ResultHandler?.Invoke(moduleId, registrationId, resultId, section);

    public void MenuChanged() => MenuChangedHandler?.Invoke();
}
