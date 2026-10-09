using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DiagnosticLabs.Application.Auth;

public sealed class AuthService(
    IAppDbContext db,
    IPasswordHasher hasher,
    ICurrentUserSession session,
    IClock clock,
    ILogger<AuthService> logger) : IAuthService
{
    public const int MaxFailedAttempts = 5;

    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    // One message for every failure so callers cannot tell a bad username from a bad password or a lockout.
    private static readonly Error InvalidCredentials =
        new("Auth.InvalidCredentials", "Login failed. Please try again.");

    public async Task<Result<AuthenticatedUser>> LoginAsync(
        string username, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
            return Result<AuthenticatedUser>.Failure(InvalidCredentials);

        var user = await db.Users
            .Include(u => u.Permissions)
            .FirstOrDefaultAsync(u => u.Username == username && u.IsActive, cancellationToken);

        // Do the same work whether or not the user exists, so timing does not reveal valid usernames.
        var verification = hasher.Verify(password, user?.PasswordHash ?? string.Empty);
        var now = clock.UtcNow;

        if (user is not null && user.LockoutEndUtc is { } lockedUntil && lockedUntil > now)
        {
            logger.LogWarning("Login rejected for locked-out user {UserId} until {LockoutEnd:u}", user.Id, lockedUntil);
            return Result<AuthenticatedUser>.Failure(InvalidCredentials);
        }

        if (user is null || verification == PasswordVerification.Failed)
        {
            logger.LogWarning("Failed login for username {Username}", username);
            if (user is not null)
                await RecordFailureAsync(user, now, cancellationToken);
            return Result<AuthenticatedUser>.Failure(InvalidCredentials);
        }

        if (verification == PasswordVerification.SuccessRehashNeeded)
        {
            // Transparently upgrade legacy unsalted SHA-1 hashes on first successful sign-in.
            user.PasswordHash = hasher.Hash(password);
            logger.LogInformation("Upgraded password hash for user {UserId}", user.Id);
        }

        user.FailedLoginCount = 0;
        user.LockoutEndUtc = null;
        user.LastLoginAtUtc = now;
        await db.SaveChangesAsync(cancellationToken);

        var authenticated = new AuthenticatedUser(
            user.Id,
            user.Username,
            user.FullName,
            user.IsAdmin,
            user.MustChangePassword,
            [.. user.Permissions.Select(p => new ModulePermission(
                p.ModuleId, p.AllowCreate, p.AllowEdit, p.AllowDelete, p.AllowPrint))]);

        session.SignIn(authenticated);
        logger.LogInformation("User {UserId} signed in", user.Id);
        return Result<AuthenticatedUser>.Success(authenticated);
    }

    private async Task RecordFailureAsync(Domain.Identity.User user, DateTime now, CancellationToken cancellationToken)
    {
        user.FailedLoginCount++;
        if (user.FailedLoginCount >= MaxFailedAttempts)
        {
            user.LockoutEndUtc = now + LockoutDuration;
            user.FailedLoginCount = 0;
            logger.LogWarning("User {UserId} locked out until {LockoutEnd:u}", user.Id, user.LockoutEndUtc);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
