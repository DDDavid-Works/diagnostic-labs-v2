using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DiagnosticLabs.Application.Auth;

public interface IAccountService
{
    /// <summary>Lets the signed-in user replace their own password. Available to everyone; it is not a permission module.</summary>
    Task<Result> ChangePasswordAsync(
        string oldPassword, string newPassword, string confirmPassword, CancellationToken cancellationToken = default);
}

public sealed class AccountService(
    IAppDbContext db,
    IPasswordHasher hasher,
    ICurrentUserSession session,
    ILogger<AccountService> logger) : IAccountService
{
    public async Task<Result> ChangePasswordAsync(
        string oldPassword, string newPassword, string confirmPassword, CancellationToken cancellationToken = default)
    {
        if (session.UserId is not { } userId)
            return Result.Failure(Errors.Forbidden);

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
            return Result.Failure(Errors.NotFound("User"));

        if (hasher.Verify(oldPassword ?? string.Empty, user.PasswordHash) == PasswordVerification.Failed)
            return Result.Failure(new Error("Account.WrongPassword", "The current password is not correct."));

        var errors = PasswordPolicy.Validate(newPassword, user.Username).ToList();
        if (errors.Count == 0 && newPassword != confirmPassword)
            errors.Add("The new password and its confirmation do not match.");
        if (errors.Count == 0 && newPassword == oldPassword)
            errors.Add("The new password must be different from the current one.");
        if (errors.Count > 0)
            return Result.Failure(Errors.Invalid(errors));

        user.PasswordHash = hasher.Hash(newPassword);
        user.MustChangePassword = false;
        user.FailedLoginCount = 0;
        user.LockoutEndUtc = null;
        await db.SaveChangesAsync(cancellationToken);

        session.MarkPasswordChanged();
        logger.LogInformation("User {UserId} changed their password", userId);
        return Result.Success();
    }
}
