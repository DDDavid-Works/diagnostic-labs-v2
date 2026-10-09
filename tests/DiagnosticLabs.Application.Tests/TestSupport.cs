using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Auth;
using DiagnosticLabs.Domain.Identity;
using DiagnosticLabs.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Application.Tests;

internal sealed class FakeClock(DateTime start) : IClock
{
    public DateTime UtcNow { get; set; } = start;
}

/// <summary>An in-memory AppDbContext wired with the same interceptors as production.</summary>
internal sealed class TestEnvironment
{
    public CurrentUserSession Session { get; } = new();

    public FakeClock Clock { get; } = new(new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc));

    public AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(new AuditSaveChangesInterceptor(Session, Clock), new AuditLogInterceptor(Session, Clock))
            .Options;
        return new AppDbContext(options);
    }

    public void SignInAs(long userId) =>
        Session.SignIn(new AuthenticatedUser(userId, "u" + userId, "User " + userId, false, false, []));

    public static User NewUser(string username, string passwordHash) => new()
    {
        Username = username,
        FullName = "Test User",
        PasswordHash = passwordHash,
    };
}
