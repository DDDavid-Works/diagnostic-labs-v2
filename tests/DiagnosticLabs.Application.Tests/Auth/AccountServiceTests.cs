using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Auth;
using DiagnosticLabs.Domain.Identity;
using DiagnosticLabs.Infrastructure.Persistence;
using DiagnosticLabs.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DiagnosticLabs.Application.Tests.Auth;

public class AccountServiceTests
{
    private readonly TestEnvironment _env = new();
    private readonly Pbkdf2PasswordHasher _hasher = new();

    private AccountService CreateService(AppDbContext db) => new(db, _hasher, _env.Session, NullLogger<AccountService>.Instance);

    private async Task<long> SeedSignedInAsync(AppDbContext db, bool mustChange = true)
    {
        var user = new User { Username = "ana", FullName = "Ana", PasswordHash = _hasher.Hash("Old-password-1"), MustChangePassword = mustChange };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        _env.Session.SignIn(new AuthenticatedUser(user.Id, user.Username, user.FullName, false, mustChange, []));
        return user.Id;
    }

    [Fact]
    public async Task Changing_the_password_stores_a_new_hash_and_clears_the_must_change_flag_everywhere()
    {
        await using var db = _env.CreateDb();
        var id = await SeedSignedInAsync(db);

        var result = await CreateService(db).ChangePasswordAsync("Old-password-1", "New-password-2", "New-password-2");

        Assert.True(result.IsSuccess);
        var stored = await db.Users.AsNoTracking().SingleAsync(u => u.Id == id);
        Assert.False(stored.MustChangePassword);
        Assert.Equal(PasswordVerification.Success, _hasher.Verify("New-password-2", stored.PasswordHash));
        Assert.Equal(PasswordVerification.Failed, _hasher.Verify("Old-password-1", stored.PasswordHash));
        Assert.False(_env.Session.MustChangePassword);
    }

    [Fact]
    public async Task A_wrong_current_password_is_refused_and_nothing_changes()
    {
        await using var db = _env.CreateDb();
        var id = await SeedSignedInAsync(db);
        var before = (await db.Users.AsNoTracking().SingleAsync(u => u.Id == id)).PasswordHash;

        var result = await CreateService(db).ChangePasswordAsync("Not-my-password", "New-password-2", "New-password-2");

        Assert.Equal("Account.WrongPassword", result.Error.Code);
        Assert.Equal(before, (await db.Users.AsNoTracking().SingleAsync(u => u.Id == id)).PasswordHash);
        Assert.True(_env.Session.MustChangePassword);
    }

    [Theory]
    [InlineData("short", "short")]
    [InlineData("New-password-2", "Different-3")]
    [InlineData("Old-password-1", "Old-password-1")]
    [InlineData("ana", "ana")]
    public async Task The_new_password_must_meet_the_policy_match_its_confirmation_and_differ_from_the_old(string newPassword, string confirm)
    {
        await using var db = _env.CreateDb();
        await SeedSignedInAsync(db);

        var result = await CreateService(db).ChangePasswordAsync("Old-password-1", newPassword, confirm);

        Assert.True(result.IsFailure);
        Assert.True(_env.Session.MustChangePassword);
    }

    [Fact]
    public async Task Nobody_signed_in_can_not_change_a_password()
    {
        await using var db = _env.CreateDb();

        Assert.Equal("Auth.Forbidden", (await CreateService(db).ChangePasswordAsync("a", "b", "b")).Error.Code);
    }

    [Fact]
    public async Task Any_signed_in_user_can_change_their_password_without_a_module_permission()
    {
        await using var db = _env.CreateDb();
        await SeedSignedInAsync(db, mustChange: false); // no permissions at all

        Assert.True((await CreateService(db).ChangePasswordAsync("Old-password-1", "New-password-2", "New-password-2")).IsSuccess);
    }
}
