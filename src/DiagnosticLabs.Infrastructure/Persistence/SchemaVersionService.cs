using DiagnosticLabs.Application.Schema;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Infrastructure.Persistence;

/// <summary>
/// The schema version is EF's own migration history: the migrations compiled into this program against the
/// rows in <c>__EFMigrationsHistory</c>. Both <c>db/schema.sql</c> and <c>dotnet ef database update</c> keep it current.
/// </summary>
public sealed class SchemaVersionService(AppDbContext db) : ISchemaVersionService
{
    public async Task<SchemaCheckResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // A missing database or history table (never set up) reads as "no migrations applied".
            var applied = await db.Database.GetAppliedMigrationsAsync(cancellationToken);
            return SchemaCheck.Evaluate(db.Database.GetMigrations().ToList(), applied.ToList());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return SchemaCheck.Unreachable(ex);
        }
    }
}
