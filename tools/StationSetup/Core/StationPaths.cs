namespace DiagnosticLabs.StationSetup.Core;

/// <summary>
/// Names and folders shared with Install-Server.ps1 (share and account) and Start-DiagnosticLabs.ps1 (install folder).
/// Change them together.
/// </summary>
internal static class StationPaths
{
    public const string ShareName = "DiagnosticLabs";
    public const string ShareAccount = "dlupdate";
    public const string AppExeName = "DiagnosticLabs.Wpf.exe";
    public const string LocalConfigName = "appsettings.Local.json";
    public const string VersionFileName = "version.txt";

    public static string DataRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DiagnosticLabs");

    /// <summary>Where the launcher copies the app (Start-DiagnosticLabs.ps1 $InstallDir).</summary>
    public static string InstallDir => Path.Combine(DataRoot, "app");

    /// <summary>Per user, so setup needs no administrator rights.</summary>
    public static string LauncherDir => Path.Combine(DataRoot, "launcher");

    public static string LogFile => Path.Combine(DataRoot, "station-setup.log");

    /// <summary>
    /// Cleans what IT typed into a bare host name or IP: "\\SERVER\DiagnosticLabs" or " server " become "SERVER"/"server".
    /// Returns null when nothing usable is left.
    /// </summary>
    public static string? NormalizeHost(string? typed)
    {
        if (string.IsNullOrWhiteSpace(typed)) return null;
        var host = typed.Trim().TrimStart('\\', '/');
        var cut = host.IndexOfAny(['\\', '/']);
        if (cut >= 0) host = host[..cut];
        host = host.Trim();
        if (host.Length == 0 || host.Any(c => char.IsWhiteSpace(c) || c is ',' or ':' or '*' or '?' or '"' or '<' or '>' or '|'))
            return null;
        return host;
    }

    public static string ShareRoot(string host) => $@"\\{host}\{ShareName}";
    public static string ShareCurrent(string host) => $@"{ShareRoot(host)}\current";
    public static string ShareConfig(string host) => $@"{ShareRoot(host)}\config\{LocalConfigName}";

    /// <summary>The account as stations sign in with it: the server's own local account.</summary>
    public static string ShareUser(string host) => $@"{host}\{ShareAccount}";
}
