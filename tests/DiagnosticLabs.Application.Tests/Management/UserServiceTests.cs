using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Auth;
using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Application.Management;
using DiagnosticLabs.Domain.Auditing;
using DiagnosticLabs.Domain.Identity;
using DiagnosticLabs.Infrastructure.Persistence;
using DiagnosticLabs.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Application.Tests.Management;

public class UserServiceTests
{
    private const int Patients = ModuleIds.Patients;
    private const int SalesReport = 24;
    private const int Users = ModuleIds.Users;

    private readonly TestEnvironment _env = new();
    private readonly Pbkdf2PasswordHasher _hasher = new();

    private UserService CreateService(AppDbContext db) => new(db, _env.Session, _hasher, _env.Clock);

    /// <summary>Seeds the module tree and a signed-in administrator; returns the admin's id.</summary>
    private async Task<long> SeedAsync(AppDbContext db)
    {
        var normal = new ModuleType { Id = 1, ModuleTypeName = "Registration", SortOrder = 1, IsActive = true };
        var settings = new ModuleType { Id = 2, ModuleTypeName = "Settings", SortOrder = 2, IsActive = true, IsAdmin = true };
        db.Modules.AddRange(
            new Module { Id = Patients, ModuleName = "Patients", ModuleType = normal, SortOrder = 1, IsActive = true, HasCreate = true, HasEdit = true, HasDelete = true, HasPrint = true },
            new Module { Id = SalesReport, ModuleName = "Sales Report", ModuleType = normal, SortOrder = 2, IsActive = true, HasPrint = true },
            new Module { Id = Users, ModuleName = "Users", ModuleType = settings, SortOrder = 1, IsActive = true, HasCreate = true, HasEdit = true, HasDelete = true },
            new Module { Id = ModuleIds.ChangePassword, ModuleName = "Change Password", ModuleType = settings, SortOrder = 2, IsActive = true, HasEdit = true },
            new Module { Id = 99, ModuleName = "Retired", ModuleType = normal, SortOrder = 3, IsActive = false, HasEdit = true });

        var admin = new User { Username = "boss", FullName = "The Boss", PasswordHash = _hasher.Hash("boss-password"), IsAdmin = true };
        db.Users.Add(admin);
        await db.SaveChangesAsync();

        _env.Session.SignIn(new AuthenticatedUser(admin.Id, admin.Username, admin.FullName, true, false, []));
        return admin.Id;
    }

    private static UserInput NewUser(string username = "clerk", string temp = "Temp-pass-1", bool admin = false, params PermissionLine[] lines) =>
        new(0, username, "Front Desk", admin, true, temp, lines, null);

    private static PermissionLine Access(int moduleId, bool create = false, bool edit = false, bool delete = false, bool print = false) =>
        new(moduleId, create, edit, delete, print);

    // ---------------------------------------------------------------- creating

    [Fact]
    public async Task A_new_user_gets_a_hashed_temporary_password_and_must_change_it()
    {
        await using var db = _env.CreateDb();
        await SeedAsync(db);

        var result = await CreateService(db).SaveAsync(NewUser());

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.MustChangePassword);
        var stored = await db.Users.SingleAsync(u => u.Username == "clerk");
        Assert.NotEqual("Temp-pass-1", stored.PasswordHash);
        Assert.Equal(PasswordVerification.Success, _hasher.Verify("Temp-pass-1", stored.PasswordHash));
    }

    [Fact]
    public async Task A_new_user_can_sign_in_with_the_temporary_password()
    {
        await using var db = _env.CreateDb();
        await SeedAsync(db);
        await CreateService(db).SaveAsync(NewUser(lines: Access(Patients, create: true)));
        _env.Session.SignOut();

        var login = await new AuthService(db, _hasher, _env.Session, _env.Clock, Microsoft.Extensions.Logging.Abstractions.NullLogger<AuthService>.Instance)
            .LoginAsync("clerk", "Temp-pass-1");

        Assert.True(login.IsSuccess);
        Assert.True(login.Value.MustChangePassword);
        Assert.Equal(Patients, login.Value.Permissions.Single().ModuleId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("ab")]
    [InlineData("two words")]
    public async Task User_names_must_be_3_to_100_characters_without_spaces(string username)
    {
        await using var db = _env.CreateDb();
        await SeedAsync(db);

        var result = await CreateService(db).SaveAsync(NewUser(username));

        Assert.True(result.IsFailure);
        Assert.Single(await db.Users.ToListAsync()); // only the seeded admin
    }

    [Fact]
    public async Task User_names_are_unique_ignoring_case()
    {
        await using var db = _env.CreateDb();
        await SeedAsync(db);
        var service = CreateService(db);
        await service.SaveAsync(NewUser("Maria"));

        var duplicate = await service.SaveAsync(NewUser("maria"));

        Assert.True(duplicate.IsFailure);
        Assert.Contains("already taken", duplicate.Error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("short")]
    [InlineData("clerk")]
    [InlineData("        ")]
    public async Task The_temporary_password_must_meet_the_policy(string password)
    {
        await using var db = _env.CreateDb();
        await SeedAsync(db);

        var result = await CreateService(db).SaveAsync(NewUser("clerk", password));

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Editing_a_user_never_changes_their_password()
    {
        await using var db = _env.CreateDb();
        await SeedAsync(db);
        var service = CreateService(db);
        var created = (await service.SaveAsync(NewUser())).Value;
        var before = (await db.Users.AsNoTracking().SingleAsync(u => u.Id == created.Id)).PasswordHash;

        await service.SaveAsync(new UserInput(created.Id, "clerk", "Renamed", false, true, "SomethingElse-1", [], created.RowVersion));

        var after = (await db.Users.AsNoTracking().SingleAsync(u => u.Id == created.Id)).PasswordHash;
        Assert.Equal(before, after);
    }

    // ---------------------------------------------------------------- permissions

    [Fact]
    public async Task Permissions_are_saved_updated_and_removed_and_view_only_is_a_row_with_no_actions()
    {
        await using var db = _env.CreateDb();
        await SeedAsync(db);
        var service = CreateService(db);

        var created = (await service.SaveAsync(NewUser(lines: [Access(Patients, create: true, edit: true), Access(SalesReport)]))).Value;
        Assert.Equal(2, created.Permissions.Count);
        Assert.Equal(new PermissionLine(SalesReport, false, false, false, false), created.Permissions.Single(p => p.ModuleId == SalesReport));

        var edited = (await service.SaveAsync(new UserInput(
            created.Id, "clerk", "Front Desk", false, true, null, [Access(Patients, print: true)], created.RowVersion))).Value;

        var only = Assert.Single(edited.Permissions);
        Assert.Equal(new PermissionLine(Patients, false, false, false, true), only);
        Assert.Equal(1, await db.UserPermissions.CountAsync(p => p.UserId == created.Id));
    }

    [Fact]
    public async Task Removing_access_is_recorded_in_the_audit_log()
    {
        await using var db = _env.CreateDb();
        await SeedAsync(db);
        var service = CreateService(db);
        var created = (await service.SaveAsync(NewUser(lines: Access(Patients, create: true)))).Value;

        await service.SaveAsync(new UserInput(created.Id, "clerk", "Front Desk", false, true, null, [], created.RowVersion));

        Assert.Contains(await db.AuditLogs.ToListAsync(), a => a.EntityName == nameof(UserPermission) && a.Action == AuditAction.Delete);
    }

    [Fact]
    public async Task Actions_a_module_does_not_support_are_rejected()
    {
        await using var db = _env.CreateDb();
        await SeedAsync(db);

        var result = await CreateService(db).SaveAsync(NewUser(lines: Access(SalesReport, create: true))); // Sales Report only supports Print

        Assert.True(result.IsFailure);
        Assert.Contains("does not support", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Administrator_only_modules_can_not_be_given_to_regular_users()
    {
        await using var db = _env.CreateDb();
        await SeedAsync(db);

        var result = await CreateService(db).SaveAsync(NewUser(lines: Access(Users, edit: true)));

        Assert.True(result.IsFailure);
        Assert.Contains("only for administrators", result.Error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(404)]
    [InlineData(99)] // inactive module
    [InlineData(ModuleIds.ChangePassword)]
    public async Task Unknown_inactive_or_non_permission_modules_are_rejected(int moduleId)
    {
        await using var db = _env.CreateDb();
        await SeedAsync(db);

        Assert.True((await CreateService(db).SaveAsync(NewUser(lines: Access(moduleId, edit: true)))).IsFailure);
    }

    [Fact]
    public async Task A_module_can_only_be_listed_once()
    {
        await using var db = _env.CreateDb();
        await SeedAsync(db);

        var result = await CreateService(db).SaveAsync(NewUser(lines: [Access(Patients), Access(Patients, print: true)]));

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Administrators_carry_no_permission_rows_because_they_can_open_everything()
    {
        await using var db = _env.CreateDb();
        await SeedAsync(db);
        var service = CreateService(db);

        var created = (await service.SaveAsync(NewUser("second-admin", admin: true, lines: Access(Patients, create: true)))).Value;

        Assert.True(created.IsAdmin);
        Assert.Empty(created.Permissions);
    }

    // ---------------------------------------------------------------- safeguards

    [Fact]
    public async Task You_can_not_deactivate_yourself_or_remove_your_own_admin_rights()
    {
        await using var db = _env.CreateDb();
        var adminId = await SeedAsync(db);
        var service = CreateService(db);
        await service.SaveAsync(NewUser("second-admin", admin: true)); // so the "last admin" rule is not the reason
        var me = (await service.GetAsync(adminId)).Value;

        var deactivate = await service.SaveAsync(new UserInput(adminId, me.Username, me.FullName, true, false, null, [], me.RowVersion));
        var demote = await service.SaveAsync(new UserInput(adminId, me.Username, me.FullName, false, true, null, [], me.RowVersion));
        var delete = await service.DeleteAsync(adminId);

        Assert.Contains("deactivate your own", deactivate.Error.Message, StringComparison.Ordinal);
        Assert.Contains("own administrator rights", demote.Error.Message, StringComparison.Ordinal);
        Assert.Equal("User.Self", delete.Error.Code);
    }

    [Fact]
    public async Task The_last_active_administrator_can_not_be_removed_but_another_one_can()
    {
        await using var db = _env.CreateDb();
        await SeedAsync(db);
        var service = CreateService(db);
        var second = (await service.SaveAsync(NewUser("second-admin", admin: true))).Value;

        // sign in as the second admin so the first is "someone else"
        _env.Session.SignIn(new AuthenticatedUser(second.Id, "second-admin", "Front Desk", true, false, []));
        var first = await db.Users.AsNoTracking().SingleAsync(u => u.Username == "boss");

        Assert.True((await service.DeleteAsync(first.Id)).IsSuccess); // second admin remains
        var third = (await service.SaveAsync(NewUser("third", admin: false))).Value;
        _env.Session.SignIn(new AuthenticatedUser(third.Id, "third", "x", true, false, []));

        var last = await service.DeleteAsync(second.Id); // now second is the only active admin
        Assert.Equal("User.LastAdmin", last.Error.Code);

        var demote = await service.SaveAsync(new UserInput(second.Id, "second-admin", "Front Desk", false, true, null, [], second.RowVersion));
        Assert.Contains("At least one active administrator", demote.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Deactivating_a_user_stops_them_signing_in()
    {
        await using var db = _env.CreateDb();
        await SeedAsync(db);
        var service = CreateService(db);
        var created = (await service.SaveAsync(NewUser())).Value;

        await service.DeleteAsync(created.Id);

        _env.Session.SignOut();
        var login = await new AuthService(db, _hasher, _env.Session, _env.Clock, Microsoft.Extensions.Logging.Abstractions.NullLogger<AuthService>.Instance)
            .LoginAsync("clerk", "Temp-pass-1");
        Assert.True(login.IsFailure);
    }

    // ---------------------------------------------------------------- session refresh

    [Fact]
    public async Task Editing_your_own_permissions_refreshes_the_session_but_editing_someone_else_does_not()
    {
        await using var db = _env.CreateDb();
        var adminId = await SeedAsync(db);
        var service = CreateService(db);
        var other = (await service.SaveAsync(NewUser("worker", lines: Access(Patients, edit: true)))).Value;
        Assert.False(_env.Session.Can(Patients, ModuleAction.Edit) && !_env.Session.IsAdmin);

        // editing another user leaves the signed-in admin alone
        await service.SaveAsync(new UserInput(other.Id, "worker", "Front Desk", false, true, null, [Access(Patients, print: true)], other.RowVersion));
        Assert.Equal(adminId, _env.Session.UserId);
        Assert.True(_env.Session.IsAdmin);

        // a user editing their own record (here: an admin who is allowed to) is refreshed immediately
        var me = (await service.GetAsync(adminId)).Value;
        await service.SaveAsync(new UserInput(adminId, me.Username, "Renamed Boss", true, true, null, [], me.RowVersion));
        Assert.Equal("Renamed Boss", _env.Session.FullName);
    }

    // ---------------------------------------------------------------- password reset and unlock

    [Fact]
    public async Task Resetting_a_password_sets_a_temporary_one_clears_the_lockout_and_forces_a_change()
    {
        await using var db = _env.CreateDb();
        await SeedAsync(db);
        var service = CreateService(db);
        var created = (await service.SaveAsync(NewUser())).Value;
        var user = await db.Users.SingleAsync(u => u.Id == created.Id);
        user.MustChangePassword = false;
        user.FailedLoginCount = 3;
        user.LockoutEndUtc = _env.Clock.UtcNow.AddMinutes(10);
        await db.SaveChangesAsync();

        var result = await service.ResetPasswordAsync(created.Id, "Brand-new-1");

        Assert.True(result.IsSuccess);
        var stored = await db.Users.AsNoTracking().SingleAsync(u => u.Id == created.Id);
        Assert.True(stored.MustChangePassword);
        Assert.Equal(0, stored.FailedLoginCount);
        Assert.Null(stored.LockoutEndUtc);
        Assert.Equal(PasswordVerification.Success, _hasher.Verify("Brand-new-1", stored.PasswordHash));
    }

    [Fact]
    public async Task A_reset_password_must_meet_the_policy()
    {
        await using var db = _env.CreateDb();
        await SeedAsync(db);
        var service = CreateService(db);
        var created = (await service.SaveAsync(NewUser())).Value;

        Assert.True((await service.ResetPasswordAsync(created.Id, "short")).IsFailure);
        Assert.True((await service.ResetPasswordAsync(created.Id, "clerk")).IsFailure);
        Assert.Equal("User.NotFound", (await service.ResetPasswordAsync(999, "Long-enough-1")).Error.Code);
    }

    [Fact]
    public async Task Unlocking_clears_the_lockout_and_the_list_shows_who_is_locked()
    {
        await using var db = _env.CreateDb();
        await SeedAsync(db);
        var service = CreateService(db);
        var created = (await service.SaveAsync(NewUser())).Value;
        var user = await db.Users.SingleAsync(u => u.Id == created.Id);
        user.LockoutEndUtc = _env.Clock.UtcNow.AddMinutes(10);
        await db.SaveChangesAsync();

        var locked = (await service.SearchAsync(new CrudSearch("clerk"))).Value.Items.Single();
        Assert.True(locked.IsLocked);

        await service.UnlockAsync(created.Id);

        var unlocked = (await service.SearchAsync(new CrudSearch("clerk"))).Value.Items.Single();
        Assert.False(unlocked.IsLocked);
    }

    [Fact]
    public async Task Only_users_with_edit_rights_on_Users_can_reset_or_unlock()
    {
        await using var db = _env.CreateDb();
        await SeedAsync(db);
        var service = CreateService(db);
        var created = (await service.SaveAsync(NewUser())).Value;
        _env.Session.SignIn(new AuthenticatedUser(created.Id, "clerk", "Front Desk", false, false, [new ModulePermission(Patients, true, true, true, true)]));

        Assert.Equal("Auth.Forbidden", (await service.ResetPasswordAsync(created.Id, "Long-enough-1")).Error.Code);
        Assert.Equal("Auth.Forbidden", (await service.UnlockAsync(created.Id)).Error.Code);
        Assert.Equal("Auth.Forbidden", (await service.SearchAsync(new CrudSearch(null))).Error.Code);
    }

    // ---------------------------------------------------------------- screen lookups

    [Fact]
    public async Task The_permission_catalog_groups_modules_like_the_menu_and_leaves_out_change_password_and_inactive_modules()
    {
        await using var db = _env.CreateDb();
        await SeedAsync(db);

        var groups = (await CreateService(db).GetPermissionCatalogAsync()).Value;

        Assert.Equal(["Registration", "Settings"], groups.Select(g => g.Name));
        Assert.False(groups[0].IsAdminGroup);
        Assert.True(groups[1].IsAdminGroup);
        Assert.Equal(["Patients", "Sales Report"], groups[0].Modules.Select(m => m.Name));
        Assert.Equal(["Users"], groups[1].Modules.Select(m => m.Name));
        var sales = groups[0].Modules.Single(m => m.Id == SalesReport);
        Assert.False(sales.SupportsCreate);
        Assert.True(sales.SupportsPrint);
    }

    [Fact]
    public async Task User_options_list_active_users_for_copying_permissions()
    {
        await using var db = _env.CreateDb();
        await SeedAsync(db);
        var service = CreateService(db);
        await service.SaveAsync(NewUser("alpha"));
        var gone = (await service.SaveAsync(NewUser("beta"))).Value;
        await service.DeleteAsync(gone.Id);

        var options = (await service.GetUserOptionsAsync()).Value;

        Assert.Contains(options, o => o.Name.Contains("alpha", StringComparison.Ordinal));
        Assert.DoesNotContain(options, o => o.Name.Contains("beta", StringComparison.Ordinal));
    }
}
