using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiagnosticLabs.Application.Auth;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DiagnosticLabs.Wpf.ViewModels;

public partial class LoginViewModel(IServiceScopeFactory scopeFactory, ILogger<LoginViewModel> logger) : ObservableObject
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    private string _username = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    private bool _isBusy;

    public event EventHandler? LoginSucceeded;

    private bool CanLogin() => !IsBusy && !string.IsNullOrWhiteSpace(Username) && Password.Length > 0;

    [RelayCommand(CanExecute = nameof(CanLogin))]
    private async Task LoginAsync()
    {
        IsBusy = true;
        ErrorMessage = string.Empty;

        try
        {
            // The DbContext is scoped; a short-lived scope per attempt avoids a window-lifetime context.
            using var scope = scopeFactory.CreateScope();
            var result = await scope.ServiceProvider.GetRequiredService<IAuthService>()
                .LoginAsync(Username.Trim(), Password);

            if (result.IsFailure)
            {
                ErrorMessage = result.Error.Message;
                return;
            }

            Password = string.Empty;
            LoginSucceeded?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Login failed because of an unexpected error");
            ErrorMessage = "Cannot reach the database. Check the connection and try again.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
