using System.Diagnostics;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DiagnosticLabs.Wpf.Services;

/// <summary>Looks at the release on the server's update share and can hand over to the launcher to install it.</summary>
public interface IUpdateChecker
{
    /// <summary>The release number on the update share, or <c>null</c> when this PC has no share configured or it cannot be reached right now.</summary>
    Task<string?> GetShareVersionAsync(CancellationToken cancellationToken = default);

    /// <summary>Whether the launcher that started this program is known, so "Restart now" can be offered.</summary>
    bool CanRestart { get; }

    /// <summary>Starts the launcher a few seconds from now (after this program has closed). The caller closes the main window.</summary>
    void StartRestart();
}

/// <summary>
/// The launcher (Start-DiagnosticLabs.ps1) tells the program where the share is and where it lives through the environment variables
/// <c>Update__SharePath</c> and <c>Update__Launcher</c>, so nothing has to be configured per station. A program started any other way
/// (a developer's build) has neither, and the update notice never shows.
/// </summary>
public sealed class UpdateChecker(IConfiguration configuration, ILogger<UpdateChecker> logger) : IUpdateChecker
{
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(8);

    private string? SharePath => configuration["Update:SharePath"];

    private string? Launcher => configuration["Update:Launcher"];

    public bool CanRestart => !string.IsNullOrWhiteSpace(Launcher) && File.Exists(Launcher);

    public async Task<string?> GetShareVersionAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(SharePath))
            return null;

        var file = Path.Combine(SharePath, "version.txt");

        // A share on a server that is down can block for a long time; give up after a few seconds and ask again at the next check.
        var read = Task.Run(() => File.Exists(file) ? File.ReadAllText(file).Trim() : null, cancellationToken);
        var finished = await Task.WhenAny(read, Task.Delay(ReadTimeout, cancellationToken));
        if (finished != read)
        {
            logger.LogWarning("The update share did not answer within {Seconds} seconds", ReadTimeout.TotalSeconds);
            return null;
        }

        try
        {
            var text = await read;
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "The update share could not be read");
            return null;
        }
    }

    public void StartRestart()
    {
        // The launcher refuses to replace files under a running program, so it starts a few seconds after this one has closed.
        var start = new ProcessStartInfo("cmd.exe", $"/c ping -n 6 127.0.0.1 >nul & \"{Launcher}\"")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
        };
        Process.Start(start);
    }
}
