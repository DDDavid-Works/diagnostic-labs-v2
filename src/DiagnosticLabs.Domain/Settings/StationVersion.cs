namespace DiagnosticLabs.Domain.Settings;

/// <summary>
/// One row per PC that runs the app: which version it was on when last seen. Written by the app itself at sign-in and every few minutes while it is open,
/// so an administrator can see which stations have not picked up a release yet. Plain bookkeeping, not audited.
/// </summary>
public class StationVersion
{
    public int Id { get; set; }

    /// <summary>The Windows computer name of the station.</summary>
    public string StationName { get; set; } = string.Empty;

    public string AppVersion { get; set; } = string.Empty;

    /// <summary>The username signed in on that PC when it was last seen.</summary>
    public string? LastUserName { get; set; }

    public DateTime FirstSeenUtc { get; set; }

    public DateTime LastSeenUtc { get; set; }
}
