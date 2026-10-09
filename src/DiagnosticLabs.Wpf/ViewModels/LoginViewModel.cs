using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiagnosticLabs.Application.Auth;
using DiagnosticLabs.Application.Settings;
using DiagnosticLabs.Wpf.Services;
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

    /// <summary>The laboratory name, tagline and logo shown above the sign-in form (set by Company Setup).</summary>
    [ObservableProperty]
    private string _brandName = "Diagnostic Labs";

    [ObservableProperty]
    private string _brandTagline = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLogo))]
    private ImageSource? _logoImage;

    public bool HasLogo => LogoImage is not null;

    public event EventHandler? LoginSucceeded;

    /// <summary>Best effort: if the database can not be reached the plain title stays and the sign-in itself reports the problem.</summary>
    public async Task LoadBrandingAsync()
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var branding = await scope.ServiceProvider.GetRequiredService<ICompanySetupService>().GetBrandingAsync();
            if (!string.IsNullOrWhiteSpace(branding.CompanyName))
                BrandName = branding.CompanyName;
            BrandTagline = branding.Tagline ?? string.Empty;
            LogoImage = ImageLoader.FromBytes(branding.Logo);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not load the company branding for the sign-in window");
        }
    }

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
