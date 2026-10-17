using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Domain.Settings;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Application.Stations;

/// <summary>What the running program knows about itself (the Windows app supplies it).</summary>
public interface IAppInfo
{
    /// <summary>The release number this program was built as (the same text the launcher compares with the server's version.txt).</summary>
    string Version { get; }

    /// <summary>The Windows computer name of this PC.</summary>
    string StationName { get; }
}

public enum StationState
{
    /// <summary>On the latest release.</summary>
    UpToDate,

    /// <summary>On another release than the latest one: it has not picked up the update yet.</summary>
    Behind,

    /// <summary>The latest release is not known (the update share could not be read and no station reported a newer one).</summary>
    Unknown,
}

/// <param name="IsOpenNow">Seen within the last few minutes, so the app is most likely open on that PC right now.</param>
public sealed record StationRow(string StationName, string AppVersion, string? LastUserName, DateTime LastSeenUtc, StationState State, bool IsOpenNow);

public interface IStationVersionService
{
    /// <summary>Records that this PC runs this version now (sign-in, then every few minutes). Never needs a permission and never throws for a database problem.</summary>
    Task RecordSeenAsync(CancellationToken cancellationToken = default);

    /// <summary>The stations, newest activity first. <paramref name="latestVersion"/> is the release on the update share (<c>null</c> when it cannot be read).</summary>
    Task<Result<IReadOnlyList<StationRow>>> GetStationsAsync(string? latestVersion, CancellationToken cancellationToken = default);
}

public sealed class StationVersionService(IAppDbContext db, IAppInfo app, ICurrentUser user, IClock clock) : IStationVersionService
{
    /// <summary>The app reports every few minutes; a station is "open now" when its last report is younger than twice that.</summary>
    public static readonly TimeSpan OpenWindow = TimeSpan.FromMinutes(25);

    public async Task RecordSeenAsync(CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        var name = Trim(app.StationName, 100);
        var version = Trim(app.Version, 50);
        var who = user.IsAuthenticated ? Trim(user.FullName, 200) : null;

        var row = await db.StationVersions.FirstOrDefaultAsync(s => s.StationName == name, cancellationToken);
        if (row is null)
        {
            db.StationVersions.Add(new StationVersion { StationName = name, AppVersion = version, LastUserName = who, FirstSeenUtc = now, LastSeenUtc = now });
        }
        else
        {
            row.AppVersion = version;
            row.LastUserName = who ?? row.LastUserName;
            row.LastSeenUtc = now;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<Result<IReadOnlyList<StationRow>>> GetStationsAsync(string? latestVersion, CancellationToken cancellationToken = default)
    {
        if (!user.CanAccess(ModuleIds.Stations))
            return Result<IReadOnlyList<StationRow>>.Failure(Errors.Forbidden);

        var rows = await db.StationVersions.AsNoTracking().OrderByDescending(s => s.LastSeenUtc).ToListAsync(cancellationToken);
        var latest = string.IsNullOrWhiteSpace(latestVersion) ? NewestOf(rows.Select(r => r.AppVersion)) : latestVersion.Trim();
        var now = clock.UtcNow;

        IReadOnlyList<StationRow> list =
        [
            .. rows.Select(r => new StationRow(
                r.StationName,
                r.AppVersion,
                r.LastUserName,
                r.LastSeenUtc,
                latest is null ? StationState.Unknown : r.AppVersion == latest ? StationState.UpToDate : StationState.Behind,
                now - r.LastSeenUtc <= OpenWindow)),
        ];
        return Result<IReadOnlyList<StationRow>>.Success(list);
    }

    /// <summary>The highest of the given release numbers ("1.0.10" is newer than "1.0.9"); text order when they are not plain numbers.</summary>
    public static string? NewestOf(IEnumerable<string> versions)
    {
        string? best = null;
        foreach (var v in versions.Where(v => !string.IsNullOrWhiteSpace(v)))
            best = best is null || Compare(v, best) > 0 ? v : best;
        return best;
    }

    public static int Compare(string a, string b) =>
        Version.TryParse(a, out var va) && Version.TryParse(b, out var vb)
            ? va.CompareTo(vb)
            : string.Compare(a, b, StringComparison.OrdinalIgnoreCase);

    private static string Trim(string text, int max) => text.Length <= max ? text : text[..max];
}
