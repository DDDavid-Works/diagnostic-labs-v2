using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;

namespace DiagnosticLabs.Wpf.ViewModels;

/// <summary>Shared behaviour of screens: busy flag, a status message, and a safe wrapper for async commands.</summary>
public abstract partial class ViewModelBase(ILogger logger) : ObservableObject
{
    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _message = string.Empty;

    [ObservableProperty]
    private bool _isError;

    protected void ShowInfo(string message)
    {
        Message = message;
        IsError = false;
    }

    protected void ShowError(string message)
    {
        Message = message;
        IsError = true;
    }

    protected void ClearMessage()
    {
        Message = string.Empty;
        IsError = false;
    }

    /// <summary>Runs <paramref name="action"/> once at a time; any unexpected exception is logged and shown instead of crashing.</summary>
    protected async Task RunAsync(Func<Task> action)
    {
        if (IsBusy)
            return;

        IsBusy = true;
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error in {ViewModel}", GetType().Name);
            ShowError("Something went wrong. The error was logged.");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
