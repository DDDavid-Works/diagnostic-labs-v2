namespace DiagnosticLabs.Application.Schema;

public enum SchemaStatus
{
    Current,
    /// <summary>The database lacks migrations this program needs (not set up, or an update was not run).</summary>
    DatabaseBehind,
    /// <summary>The database has migrations this program does not know (this station has an older program).</summary>
    DatabaseAhead,
    Unreachable,
}

/// <param name="Message">Plain wording for lab staff.</param>
/// <param name="Detail">Migration names or the connection error, for the log.</param>
public sealed record SchemaCheckResult(SchemaStatus Status, string Message, string? Detail = null)
{
    public bool IsCurrent => Status == SchemaStatus.Current;
}

/// <summary>Checks at start-up that the database is at the version this program was built for.</summary>
public interface ISchemaVersionService
{
    Task<SchemaCheckResult> CheckAsync(CancellationToken cancellationToken = default);
}

public static class SchemaCheck
{
    /// <summary>Compares the migrations compiled into the program with those recorded in the database.</summary>
    public static SchemaCheckResult Evaluate(IReadOnlyCollection<string> known, IReadOnlyCollection<string> applied)
    {
        var unknown = applied.Except(known, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        if (unknown.Count > 0)
        {
            return new SchemaCheckResult(
                SchemaStatus.DatabaseAhead,
                "This program is older than the database. Close it and open Diagnostic Labs from its shortcut so it can update itself. If this message comes back, ask your administrator.",
                "Database has migrations this program does not know: " + string.Join(", ", unknown));
        }

        var missing = known.Except(applied, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        if (missing.Count > 0)
        {
            var notSetUp = applied.Count == 0;
            return new SchemaCheckResult(
                SchemaStatus.DatabaseBehind,
                notSetUp
                    ? "The database has not been set up yet. Ask your administrator to install it."
                    : "The database is older than this program. Ask your administrator to update the database, then open Diagnostic Labs again.",
                "Database is missing migrations: " + string.Join(", ", missing));
        }

        return new SchemaCheckResult(SchemaStatus.Current, string.Empty);
    }

    public static SchemaCheckResult Unreachable(Exception exception) => new(
        SchemaStatus.Unreachable,
        "Diagnostic Labs cannot reach the database. Check that this PC is connected to the network and that the server is on, then try again. If it keeps happening, ask your administrator.",
        exception.Message);
}
