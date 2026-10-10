namespace DiagnosticLabs.Wpf.Services;

/// <summary>Lets one screen send the user to another (e.g. "Pay now" on a registration) without knowing about the main window.</summary>
public interface INavigationService
{
    /// <summary>Set by the main window; opens Payments with this registration already loaded.</summary>
    Action<long>? PaymentHandler { get; set; }

    /// <summary>Set by the main window; opens a result screen (module) for a registration: the given result when there is one, else a new one.</summary>
    Action<int, long, long?>? ResultHandler { get; set; }

    void OpenPayment(long registrationId);

    void OpenResult(int moduleId, long registrationId, long? resultId);
}

public sealed class NavigationService : INavigationService
{
    public Action<long>? PaymentHandler { get; set; }

    public Action<int, long, long?>? ResultHandler { get; set; }

    public void OpenPayment(long registrationId) => PaymentHandler?.Invoke(registrationId);

    public void OpenResult(int moduleId, long registrationId, long? resultId) => ResultHandler?.Invoke(moduleId, registrationId, resultId);
}
