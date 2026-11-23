using System.Runtime.InteropServices;

namespace DiagnosticLabs.StationSetup.Core;

/// <summary>"Diagnostic Labs" shortcuts on the desktop and in the Start menu, pointing at the launcher.</summary>
internal static class Shortcuts
{
    public const string Name = "Diagnostic Labs.lnk";

    public static IReadOnlyList<string> Create(string launcherDir)
    {
        var made = new List<string>();
        foreach (var folder in new[] { Environment.SpecialFolder.DesktopDirectory, Environment.SpecialFolder.Programs })
        {
            var dir = Environment.GetFolderPath(folder);
            if (string.IsNullOrEmpty(dir)) continue;
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, Name);
            Write(path, launcherDir);
            made.Add(path);
        }
        return made;
    }

    private static void Write(string path, string launcherDir)
    {
        var shellType = Type.GetTypeFromProgID("WScript.Shell") ?? throw new InvalidOperationException("Windows Script Host is not available.");
        dynamic shell = Activator.CreateInstance(shellType)!;
        try
        {
            dynamic link = shell.CreateShortcut(path);
            try
            {
                link.TargetPath = Path.Combine(launcherDir, LauncherFiles.CmdName);
                link.WorkingDirectory = launcherDir;
                link.IconLocation = Path.Combine(launcherDir, LauncherFiles.IconName) + ",0";
                link.Description = "Diagnostic Labs";
                link.WindowStyle = 7; // minimized: the .cmd's console does not flash up
                link.Save();
            }
            finally
            {
                Marshal.FinalReleaseComObject(link);
            }
        }
        finally
        {
            Marshal.FinalReleaseComObject(shell);
        }
    }
}
