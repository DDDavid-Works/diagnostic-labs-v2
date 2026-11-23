namespace DiagnosticLabs.StationSetup.Core;

/// <summary>A plain text trail for whoever troubleshoots a station later. Never given a password.</summary>
internal static class SetupLog
{
    public static void Write(string line)
    {
        try
        {
            Directory.CreateDirectory(StationPaths.DataRoot);
            File.AppendAllText(StationPaths.LogFile, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {Environment.MachineName}\\{Environment.UserName}  {line}\r\n");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The log must never stop the setup.
        }
    }
}
