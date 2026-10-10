namespace DiagnosticLabs.Wpf.ViewModels;

/// <summary>A result screen that the home screen can open on a registration (a new result) or on a result that was already made.</summary>
public interface ILabResultScreen
{
    void RequestRegistration(long registrationId);

    void RequestResult(long resultId);
}
