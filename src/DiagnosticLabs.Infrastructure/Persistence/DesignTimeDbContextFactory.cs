using DiagnosticLabs.Application.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DiagnosticLabs.Infrastructure.Persistence;

/// <summary>
/// Used only by <c>dotnet ef</c>. Set <c>DL_MIGRATION_CONNECTION</c> to target a real server;
/// the default is never contacted when only generating migrations or scripts.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("DL_MIGRATION_CONNECTION")
                         ?? "Server=localhost;Database=DiagnosticLabsDB_Design;Trusted_Connection=True;TrustServerCertificate=True";

        var session = new CurrentUserSession();
        var clock = new SystemClock();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connection)
            .AddInterceptors(new AuditSaveChangesInterceptor(session, clock), new AuditLogInterceptor(session, clock))
            .Options;
        return new AppDbContext(options);
    }
}
