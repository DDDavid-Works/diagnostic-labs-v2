using System.Text;

namespace DiagnosticLabs.StationSetup.Core;

/// <summary>
/// The launcher's first copy, done here with a progress bar: mirrors the share's release into the install folder,
/// keeps the station's appsettings.Local.json, and writes version.txt last (as Start-DiagnosticLabs.ps1 does).
/// </summary>
internal static class AppInstaller
{
    private static readonly string[] Kept = [StationPaths.LocalConfigName, StationPaths.VersionFileName];

    public static string? ReadVersion(string folder)
    {
        var file = Path.Combine(folder, StationPaths.VersionFileName);
        return File.Exists(file) ? File.ReadAllText(file).Trim() : null;
    }

    /// <summary>Copies the release; reports progress 0..1. Returns the version installed.</summary>
    public static string Mirror(string source, string target, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        var version = ReadVersion(source) ?? throw new IOException($"{source} has no {StationPaths.VersionFileName}: the release is not complete.");

        Directory.CreateDirectory(target);
        // No version.txt while copying: an interrupted copy is redone next time, never run.
        File.Delete(Path.Combine(target, StationPaths.VersionFileName));

        var files = Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(source, f))
            .Where(r => !IsKept(r) && !r.StartsWith("_server" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var wanted = new HashSet<string>(files, StringComparer.OrdinalIgnoreCase);

        long total = files.Sum(r => new FileInfo(Path.Combine(source, r)).Length), done = 0;
        foreach (var rel in files)
        {
            ct.ThrowIfCancellationRequested();
            var from = new FileInfo(Path.Combine(source, rel));
            var to = new FileInfo(Path.Combine(target, rel));
            if (!(to.Exists && to.Length == from.Length && to.LastWriteTimeUtc == from.LastWriteTimeUtc))
            {
                Directory.CreateDirectory(to.DirectoryName!);
                from.CopyTo(to.FullName, overwrite: true);
            }
            done += from.Length;
            progress?.Report(total == 0 ? 1 : (double)done / total);
        }

        // Mirror: drop files the release no longer has.
        foreach (var stale in Directory.EnumerateFiles(target, "*", SearchOption.AllDirectories)
                     .Select(f => Path.GetRelativePath(target, f))
                     .Where(r => !IsKept(r) && !wanted.Contains(r))
                     .ToList())
            File.Delete(Path.Combine(target, stale));

        File.WriteAllText(Path.Combine(target, StationPaths.VersionFileName), version + "\r\n", Encoding.ASCII);
        return version;
    }

    /// <summary>First run on this PC: the connection string comes from the server's config folder. Never overwrites.</summary>
    public static bool SeedConfig(string configOnShare, string target)
    {
        var local = Path.Combine(target, StationPaths.LocalConfigName);
        if (File.Exists(local)) return false;
        File.Copy(configOnShare, local);
        return true;
    }

    private static bool IsKept(string relative) => Kept.Contains(relative, StringComparer.OrdinalIgnoreCase);
}
