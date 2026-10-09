using System.Windows;
using DiagnosticLabs.Wpf.ViewModels;
using DiagnosticLabs.Wpf.Views;
using Microsoft.Extensions.DependencyInjection;

namespace DiagnosticLabs.Wpf.Services;

public interface IPasswordChangeDialog
{
    /// <param name="forced">True right after sign-in with a temporary password: the user can not carry on without changing it.</param>
    /// <returns>Whether the password was changed.</returns>
    bool Show(bool forced);
}

public sealed class PasswordChangeDialog(IServiceProvider services) : IPasswordChangeDialog
{
    public bool Show(bool forced)
    {
        var window = new ChangePasswordWindow(ActivatorUtilities.CreateInstance<ChangePasswordViewModel>(services, forced))
        {
            // The sign-in window is still open at this point when the change is forced; centre on whatever is active.
            Owner = System.Windows.Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive),
            ShowInTaskbar = forced,
        };

        if (window.Owner is null)
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;

        window.ShowDialog();
        return window.Changed;
    }
}
