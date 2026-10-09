namespace DiagnosticLabs.Wpf.Services;

/// <summary>Lets one screen send the user to another (e.g. "Pay now" on a registration) without knowing about the main window.</summary>
public interface INavigationService
{
    /// <summary>Set by the main window; opens Payments with this registration already loaded.</summary>
    Action<long>? PaymentHandler { get; set; }

    void OpenPayment(long registrationId);
}

public sealed class NavigationService : INavigationService
{
    public Action<long>? PaymentHandler { get; set; }

    public void OpenPayment(long registrationId) => PaymentHandler?.Invoke(registrationId);
}