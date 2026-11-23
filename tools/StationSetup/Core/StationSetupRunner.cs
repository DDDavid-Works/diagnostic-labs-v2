using System.ComponentModel;
using System.Diagnostics;

namespace DiagnosticLabs.StationSetup.Core;

internal enum StepState { Waiting, Running, Ok, Warning, Failed }

internal sealed record StepReport(int Index, StepState State, string Detail);

/// <summary>
/// Sets up this PC for the signed-in Windows user, one step at a time, and stops at the first step that fails
/// with a plain-language reason. Safe to run again: every step checks or overwrites, nothing piles up.
/// </summary>
internal sealed class StationSetupRunner(string host, string password, Action<StepReport> report, IProgress<double> copyProgress)
{
    public static readonly string[] Steps =
    [
        "Windows 10 or 11, 64-bit",
        "Find the server",
        "Reach the file share (port 445)",
        "Save the share login for this Windows user",
        "Read the program on the server",
        "Reach the database (SQL Server)",
        "Install the launcher",
        "Create the shortcuts",
        "Copy Diagnostic Labs to this PC",
    ];

    private static readonly TimeSpan PortTimeout = TimeSpan.FromSeconds(4);

    public async Task<bool> RunAsync(CancellationToken ct = default)
    {
        SetupLog.Write($"Setup started for server {host}");
        var steps = new Func<CancellationToken, Task<(StepState, string)>>[]
        {
            _ => Task.FromResult(CheckWindows()),
            FindServerAsync,
            ReachShareAsync,
            _ => Task.FromResult(SaveLogin()),
            _ => Task.FromResult(ReadRelease()),
            ReachDatabaseAsync,
            _ => Task.FromResult(InstallLauncher()),
            _ => Task.FromResult(CreateShortcuts()),
            c => Task.Run(() => CopyApp(c), c),
        };

        for (var i = 0; i < steps.Length; i++)
        {
            report(new StepReport(i, StepState.Running, ""));
            (StepState state, string detail) result;
            try { result = await steps[i](ct); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                result = (StepState.Failed, ex.Message);
            }
            report(new StepReport(i, result.state, result.detail));
            SetupLog.Write($"{Steps[i]}: {result.state} {result.detail}");
            if (result.state == StepState.Failed) return false;
        }
        SetupLog.Write("Setup finished");
        return true;
    }

    private static (StepState, string) CheckWindows()
    {
        var v = Environment.OSVersion.Version;
        var name = v.Build >= 22000 ? "Windows 11" : "Windows 10";
        if (v.Major < 10) return (StepState.Failed, "Diagnostic Labs needs Windows 10 or 11. This PC must be upgraded.");
        if (!Environment.Is64BitOperatingSystem) return (StepState.Failed, "Diagnostic Labs needs 64-bit Windows.");
        return v.Build < 19045 && v.Build < 22000
            ? (StepState.Warning, $"{name} build {v.Build}: works, but update Windows when you can (22H2 or later).")
            : (StepState.Ok, $"{name}, build {v.Build}");
    }

    private async Task<(StepState, string)> FindServerAsync(CancellationToken ct)
    {
        var addresses = await Network.ResolveAsync(host, ct);
        return addresses.Length == 0
            ? (StepState.Failed, $"This PC cannot find \"{host}\". Check the spelling, or type the server's IP address instead (the Server tab on the server shows it).")
            : (StepState.Ok, string.Join(", ", addresses.Select(a => a.ToString())));
    }

    private async Task<(StepState, string)> ReachShareAsync(CancellationToken ct) =>
        await Network.CanConnectAsync(host, 445, PortTimeout, ct)
            ? (StepState.Ok, "File sharing answers")
            : (StepState.Failed, "The server does not answer on port 445. Check that it is switched on, on the same network, and that the network is set to Private on both PCs.");

    private (StepState, string) SaveLogin()
    {
        ShareCredential.Save(host, StationPaths.ShareUser(host), password);
        return (StepState.Ok, $"Saved as {StationPaths.ShareUser(host)} for {Environment.UserName}");
    }

    private (StepState, string) ReadRelease()
    {
        try
        {
            var version = AppInstaller.ReadVersion(StationPaths.ShareCurrent(host));
            if (version is null)
                return (StepState.Failed, "The share has no finished release yet (current\\version.txt is missing). Put the release on the server first.");
            if (!File.Exists(StationPaths.ShareConfig(host)))
                return (StepState.Failed, "The share has no config\\appsettings.Local.json (the database settings). Run Install-Server.ps1 on the server.");
            return (StepState.Ok, $"Version {version} is on the server");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return (StepState.Failed, ShareError(ex));
        }
    }

    private async Task<(StepState, string)> ReachDatabaseAsync(CancellationToken ct)
    {
        var address = DatabaseAddress.FromSettingsJson(await File.ReadAllTextAsync(StationPaths.ShareConfig(host), ct));
        if (address is null) return (StepState.Failed, "The database settings on the server have no usable server name.");
        if ((await Network.ResolveAsync(address.Host, ct)).Length == 0)
            return (StepState.Failed, $"The database settings name the server \"{address.Host}\", which this PC cannot find. The server administrator must change config\\appsettings.Local.json on the server to use the server's IP address.");
        return await Network.CanConnectAsync(address.Host, address.Port, PortTimeout, ct)
            ? (StepState.Ok, $"{address.Host}, port {address.Port}")
            : (StepState.Failed, $"SQL Server on {address.Host} does not answer on port {address.Port}. Check that its service is running and the server's firewall allows the port.");
    }

    private (StepState, string) InstallLauncher()
    {
        LauncherFiles.Write(StationPaths.LauncherDir, StationPaths.ShareCurrent(host));
        return (StepState.Ok, StationPaths.LauncherDir);
    }

    private static (StepState, string) CreateShortcuts()
    {
        var made = Shortcuts.Create(StationPaths.LauncherDir);
        return (StepState.Ok, made.Count == 2 ? "Desktop and Start menu" : string.Join(", ", made));
    }

    private (StepState, string) CopyApp(CancellationToken ct)
    {
        var source = StationPaths.ShareCurrent(host);
        var target = StationPaths.InstallDir;
        var latest = AppInstaller.ReadVersion(source);
        var installed = AppInstaller.ReadVersion(target);
        var seeded = false;

        if (latest == installed && File.Exists(Path.Combine(target, StationPaths.AppExeName)))
        {
            copyProgress.Report(1);
        }
        else if (IsAppRunning(target))
        {
            return (StepState.Warning, $"Diagnostic Labs is open on this PC. Close it and start it from the shortcut: it updates itself to {latest}.");
        }
        else
        {
            installed = AppInstaller.Mirror(source, target, copyProgress, ct);
        }

        try { seeded = AppInstaller.SeedConfig(StationPaths.ShareConfig(host), target); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return (StepState.Failed, "Could not copy the database settings: " + ex.Message);
        }
        return (StepState.Ok, $"Version {installed}" + (seeded ? ", database settings copied" : ""));
    }

    private static bool IsAppRunning(string installDir)
    {
        var exe = Path.Combine(installDir, StationPaths.AppExeName);
        foreach (var p in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(StationPaths.AppExeName)))
        {
            using (p)
            {
                try { if (string.Equals(p.MainModule?.FileName, exe, StringComparison.OrdinalIgnoreCase)) return true; }
                catch (Win32Exception) { return true; } // cannot look inside: assume it is ours rather than copy under it
            }
        }
        return false;
    }

    /// <summary>Turns the Windows error behind a failed share read into what to do about it.</summary>
    internal static string ShareError(Exception ex) => (ex.HResult & 0xFFFF) switch
    {
        1326 or 86 => $"The server refused the password for {StationPaths.ShareAccount}. Check it on the server (Server tab, Show password) and try again.",
        1219 => "Windows on this PC is already connected to the server with another account. Sign out of Windows (or restart), sign in again, and run setup again.",
        1331 => $"The {StationPaths.ShareAccount} account is disabled on the server. Set a new password on the server's Server tab (that also enables it).",
        1330 or 1907 => $"The {StationPaths.ShareAccount} password has expired on the server. Set a new password on the server's Server tab.",
        1909 => $"The {StationPaths.ShareAccount} account is locked on the server after wrong passwords. Wait 10 minutes, or set a new password on the server.",
        53 or 51 => "The server's file share cannot be reached. Check that file sharing is on and the network is Private on the server.",
        67 => $"The server has no share named {StationPaths.ShareName}. Run Install-Server.ps1 on the server.",
        5 => $"The {StationPaths.ShareAccount} account may not read the share. Run Install-Server.ps1 on the server again (it repairs the permissions).",
        _ => ex.Message,
    };
}
