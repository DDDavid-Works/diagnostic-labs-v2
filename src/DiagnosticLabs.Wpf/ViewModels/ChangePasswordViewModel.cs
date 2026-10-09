using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiagnosticLabs.Application.Auth;
using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Wpf.Services;
using Microsoft.Extensions.Logging;

namespace DiagnosticLabs.Wpf.ViewModels;

/// <summary>
/// Change the signed-in user's own password. When <paramref name="isForced"/> the user is holding a temporary
/// password and can not continue until they replace it; the only other way out is signing out.
/// </summary>
public partial class ChangePasswordViewModel(IServiceRunner runner, bool isForced, ILogger<ChangePasswordViewModel> logger)
    : ViewModelBase(logger)
{
    // The three password boxes can not be bound; the window copies what is typed into these.
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _oldPassword = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _newPassword = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _confirmPassword = string.Empty;

    public bool IsForced { get; } = isForced;

    public string CancelText => IsForced ? "Sign out" : "Cancel";

    public string Hint => $"At least {PasswordPolicy.MinLength} characters, and different from your current password.";

    /// <summary>Raised with <c>true</c> when the password was changed, <c>false</c> when the user backed out.</summary>
    public event EventHandler<bool>? CloseRequested;

    private bool CanSave() => !IsBusy && OldPassword.Length > 0 && NewPassword.Length > 0 && ConfirmPassword.Length > 0;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private Task SaveAsync() => RunAsync(async () =>
    {
        var (old, next, confirm) = (OldPassword, NewPassword, ConfirmPassword);
        var result = await runner.RunAsync<IAccountService, Result>(s => s.ChangePasswordAsync(old, next, confirm));
        if (result.IsFailure)
        {
            ShowError(result.Error.Message);
            return;
        }

        CloseRequested?.Invoke(this, true);
    });

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(this, false);
}
