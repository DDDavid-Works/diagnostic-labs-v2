using System.Text;
using System.Text.Json;

namespace DiagnosticLabs.StationSetup.Core;

/// <summary>Writes the launcher (from tools\deploy, embedded) and its launcher.json, plus the shortcut icon.</summary>
internal static class LauncherFiles
{
    public const string CmdName = "Start-DiagnosticLabs.cmd";
    public const string ScriptName = "Start-DiagnosticLabs.ps1";
    public const string ConfigName = "launcher.json";
    public const string IconName = "app.ico";

    public static string LauncherJson(string sharePath) =>
        JsonSerializer.Serialize(new { SharePath = sharePath }, new JsonSerializerOptions { WriteIndented = true }) + "\r\n";

    /// <summary>The server the station was set up for last time, so a repair run starts filled in.</summary>
    public static string? ReadHost(string launcherDir)
    {
        try
        {
            var file = Path.Combine(launcherDir, ConfigName);
            if (!File.Exists(file)) return null;
            using var doc = JsonDocument.Parse(File.ReadAllText(file));
            return doc.RootElement.TryGetProperty("SharePath", out var p) ? StationPaths.NormalizeHost(p.GetString()) : null;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public static void Write(string launcherDir, string sharePath)
    {
        Directory.CreateDirectory(launcherDir);
        WriteResource(CmdName, launcherDir);
        WriteResource(ScriptName, launcherDir);
        WriteResource(IconName, launcherDir);
        File.WriteAllText(Path.Combine(launcherDir, ConfigName), LauncherJson(sharePath), new UTF8Encoding(false));
    }

    private static void WriteResource(string name, string dir)
    {
        using var source = typeof(LauncherFiles).Assembly.GetManifestResourceStream("Launcher." + name)
            ?? throw new InvalidOperationException($"{name} is missing from StationSetup.exe.");
        using var target = File.Create(Path.Combine(dir, name));
        source.CopyTo(target);
    }
}
