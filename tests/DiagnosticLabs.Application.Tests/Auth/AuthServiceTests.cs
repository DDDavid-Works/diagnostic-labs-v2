using System.Security.Cryptography;
using System.Text;
using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Auth;
using DiagnosticLabs.Domain.Identity;
using DiagnosticLabs.Infrastructure.Persistence;
using DiagnosticLabs.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DiagnosticLabs.Application.Tests.Auth;

public class AuthServiceTests
{
    private readonly Pbkdf2PasswordHasher _hasher = new();
    private readonly TestEnvironment _env = new();

    private AuthService CreateService(AppDbContext db) =>
        new(db, _hasher, _env.Session, _env.Clock, NullLogger<AuthService>.Instance);

    private User NewUser(string username, string password) => TestEnvironment.NewUser(username, _hasher.Hash(password));

    [Fact]
    public async Task Valid_credentials_sign_the_user_in_and_record_the_login()
    {
        await using var db = _env.CreateDb();
        db.Users.Add(NewUser("alice", "pw1"));
        await db.SaveChangesAsync();

        var result = await CreateService(db).LoginAsync("alice", "pw1");

        Assert.True(result.IsSuccess);
        Assert.True(_env.Session.IsAuthenticated);
        Assert.Equal("Test User", _env.Session.FullName);
        Assert.Equal(_env.Clock.UtcNow, (await db.Users.SingleAsync()).LastLoginAtUtc);
    }

    [Fact]
    public async Task Wrong_password_fails_and_does_not_sign_in()
    {
        await using var db = _env.CreateDb();
        db.Users.Add(NewUser("alice", "pw1"));
        await db.SaveChangesAsync();

        var result = await CreateService(db).LoginAsync("alice", "bad");

        Assert.True(result.IsFailure);
        Assert.False(_env.Session.IsAuthenticated);
        Assert.Equal(1, (await db.Users.SingleAsync()).FailedLoginCount);
    }

    [Fact]
    public async Task Unknown_user_gets_same_error_as_wrong_password()
    {
        await using var db = _env.CreateDb();
        db.Users.Add(NewUser("alice", "pw1"));
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var unknown = await service.LoginAsync("nobody", "pw1");
        var wrong = await service.LoginAsync("alice", "bad");

        Assert.Equal(wrong.Error, unknown.Error);
    }

    [Fact]
    public async Task Inactive_user_cannot_sign_in()
    {
        await using var db = _env.CreateDb();
        var user = NewUser("alice", "pw1");
        user.IsActive = false;
        db.Users.Add(user);
        await db.SaveChangesAsync();

        Assert.True((await CreateService(db).LoginAsync("alice", "pw1")).IsFailure);
    }

    [Fact]
    public async Task Repeated_failures_lock_the_account_even_for_the_right_password()
    {
        await using var db = _env.CreateDb();
        db.Users.Add(NewUser("alice", "pw1"));
        await db.SaveChangesAsync();
        var service = CreateService(db);

        for (var i = 0; i < AuthService.MaxFailedAttempts; i++)
            await service.LoginAsync("alice", "bad");

        Assert.True((await service.LoginAsync("alice", "pw1")).IsFailure);

        _env.Clock.UtcNow += AuthService.LockoutDuration + TimeSpan.FromSeconds(1);
        Assert.True((await service.LoginAsync("alice", "pw1")).IsSuccess);
        Assert.Equal(0, (await db.Users.SingleAsync()).FailedLoginCount);
    }

    [Fact]
    public async Task Legacy_sha1_user_signs_in_and_hash_is_upgraded()
    {
        var legacy = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes("pw1")));
        await using var db = _env.CreateDb();
        db.Users.Add(TestEnvironment.NewUser("alice", legacy));
        await db.SaveChangesAsync();

        var result = await CreateService(db).LoginAsync("alice", "pw1");

        Assert.True(result.IsSuccess);
        var stored = (await db.Users.SingleAsync()).PasswordHash;
        Assert.StartsWith("v1$", stored, StringComparison.Ordinal);
        Assert.Equal(PasswordVerification.Success, _hasher.Verify("pw1", stored));
    }

    [Fact]
    public async Task Permissions_are_loaded_and_admin_can_access_everything()
    {
        await using var db = _env.CreateDb();
        db.Modules.Add(new Module { Id = 5, ModuleName = "Patients", ModuleType = new ModuleType { Id = 1 } });
        var user = NewUser("bob", "pw");
        user.Permissions.Add(new UserPermission { ModuleId = 5, AllowCreate = true });
        db.Users.Add(user);
        var root = NewUser("root", "pw");
        root.IsAdmin = true;
        db.Users.Add(root);
        await db.SaveChangesAsync();
        var service = CreateService(db);

        await service.LoginAsync("bob", "pw");
        Assert.True(_env.Session.CanAccess(5));
        Assert.False(_env.Session.CanAccess(6));
        Assert.True(_env.Session.GetPermission(5)!.AllowCreate);

        await service.LoginAsync("root", "pw");
        Assert.True(_env.Session.CanAccess(6));
    }
}
