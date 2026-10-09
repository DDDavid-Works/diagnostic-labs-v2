using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Auth;
using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Application.Lookups;
using DiagnosticLabs.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Application.Management;

/// <summary>Access to one module. Having a line means the user can open the module; the flags add actions (none = view only).</summary>
public sealed record PermissionLine(int ModuleId, bool AllowCreate, bool AllowEdit, bool AllowDelete, bool AllowPrint);

public sealed record UserListItem(
    long Id, string Username, string FullName, bool IsAdmin, bool IsActive, DateTime? LastLoginAtUtc, bool IsLocked) : IHasId;

public sealed record UserDetails(
    long Id,
    string Username,
    string FullName,
    bool IsAdmin,
    bool IsActive,
    bool MustChangePassword,
    DateTime? LastLoginAtUtc,
    DateTime? LockoutEndUtc,
    bool IsLocked,
    IReadOnlyList<PermissionLine> Permissions,
    byte[] RowVersion) : IHasId;

/// <summary><see cref="TemporaryPassword"/> is required for a new user and ignored when editing (use reset password instead).</summary>
public sealed record UserInput(
    long Id,
    string? Username,
    string? FullName,
    bool IsAdmin,
    bool IsActive,
    string? TemporaryPassword,
    IReadOnlyList<PermissionLine> Permissions,
    byte[]? RowVersion) : IReferenceInput;

public sealed record PermissionModule(int Id, string Name, bool SupportsCreate, bool SupportsEdit, bool SupportsDelete, bool SupportsPrint);

public sealed record PermissionGroup(string Name, bool IsAdminGroup, IReadOnlyList<PermissionModule> Modules);

public interface IUserService : ICrudService<UserListItem, UserDetails, UserInput>
{
    /// <summary>Sets a new temporary password; the user must replace it at their next sign-in. Also clears a lockout.</summary>
    Task<Result> ResetPasswordAsync(long userId, string temporaryPassword, CancellationToken cancellationToken = default);

    Task<Result> UnlockAsync(long userId, CancellationToken cancellationToken = default);

    /// <summary>The modules a permission can be granted on, grouped like the menu.</summary>
    Task<Result<IReadOnlyList<PermissionGroup>>> GetPermissionCatalogAsync(CancellationToken cancellationToken = default);

    /// <summary>Active users, for "copy permissions from".</summary>
    Task<Result<IReadOnlyList<LookupOption>>> GetUserOptionsAsync(CancellationToken cancellationToken = default);
}

public sealed class UserService(IAppDbContext db, ICurrentUserSession session, IPasswordHasher hasher, IClock clock)
    : ReferenceCrudService<User, UserListItem, UserDetails, UserInput>(db, session, ModuleIds.Users, "User"),
      IUserService
{
    public const int UsernameMinLength = 3;
    public const int UsernameMaxLength = 100;

    protected override DbSet<User> Set => Db.Users;

    protected override IQueryable<User> ForEditing(IQueryable<User> q) => q.Include(u => u.Permissions);

    protected override IQueryable<User> Matches(IQueryable<User> q, string word) =>
        q.Where(u => u.Username.Contains(word) || u.FullName.Contains(word));

    protected override IOrderedQueryable<User> Order(IQueryable<User> q) => q.OrderBy(u => u.Username).ThenBy(u => u.Id);

    private bool IsLocked(User u) => u.LockoutEndUtc is { } until && until > clock.UtcNow;

    protected override UserListItem ToListItem(User u) =>
        new(u.Id, u.Username, u.FullName, u.IsAdmin, u.IsActive, u.LastLoginAtUtc, IsLocked(u));

    protected override UserDetails ToDetails(User u) =>
        new(u.Id, u.Username, u.FullName, u.IsAdmin, u.IsActive, u.MustChangePassword, u.LastLoginAtUtc, u.LockoutEndUtc, IsLocked(u),
            [.. u.Permissions.OrderBy(p => p.ModuleId).Select(p => new PermissionLine(p.ModuleId, p.AllowCreate, p.AllowEdit, p.AllowDelete, p.AllowPrint))],
            u.RowVersion);

    // ---------------------------------------------------------------- validation

    protected override IReadOnlyList<string> Validate(UserInput input)
    {
        var errors = new List<string>();

        var username = input.Username?.Trim() ?? string.Empty;
        if (username.Length == 0)
            errors.Add("User name can not be empty.");
        else if (username.Length < UsernameMinLength || username.Length > UsernameMaxLength)
            errors.Add($"User name must be between {UsernameMinLength} and {UsernameMaxLength} characters.");
        else if (username.Any(char.IsWhiteSpace))
            errors.Add("User name can not contain spaces.");

        Rules.Required(errors, input.FullName, "Full name", 200);

        if (input.Id == 0)
            errors.AddRange(PasswordPolicy.Validate(input.TemporaryPassword, username));

        return errors;
    }

    protected override async Task<IReadOnlyList<string>> ValidateAsync(UserInput input, CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        var username = input.Username!.Trim();

        if (await Db.Users.AnyAsync(u => u.Id != input.Id && u.Username.ToLower() == username.ToLower(), cancellationToken))
            errors.Add("That user name is already taken.");

        if (!input.IsAdmin)
            errors.AddRange(await ValidatePermissionsAsync(input.Permissions, cancellationToken));

        if (input.Id != 0)
            errors.AddRange(await ValidateSafeguardsAsync(input, cancellationToken));

        return errors;
    }

    private async Task<IReadOnlyList<string>> ValidatePermissionsAsync(IReadOnlyList<PermissionLine> lines, CancellationToken cancellationToken)
    {
        var errors = new List<string>();

        if (lines.GroupBy(l => l.ModuleId).Any(g => g.Count() > 1))
            errors.Add("A module can only be listed once.");

        var ids = lines.Select(l => l.ModuleId).Distinct().ToList();
        var modules = await Db.Modules.Include(m => m.ModuleType).Where(m => ids.Contains(m.Id)).ToListAsync(cancellationToken);

        foreach (var line in lines.DistinctBy(l => l.ModuleId))
        {
            var module = modules.FirstOrDefault(m => m.Id == line.ModuleId);
            if (module is null || !module.IsActive || module.Id == ModuleIds.ChangePassword)
            {
                errors.Add($"Module {line.ModuleId} is not available.");
                continue;
            }

            if (module.ModuleType.IsAdmin)
                errors.Add($"{module.ModuleName} is only for administrators.");

            if ((line.AllowCreate && !module.HasCreate) || (line.AllowEdit && !module.HasEdit)
                || (line.AllowDelete && !module.HasDelete) || (line.AllowPrint && !module.HasPrint))
                errors.Add($"{module.ModuleName} does not support one of the selected actions.");
        }

        return errors;
    }

    // Nobody can lock the app out of its own administration, or cut off the branch they are sitting on.
    private async Task<IReadOnlyList<string>> ValidateSafeguardsAsync(UserInput input, CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        var existing = await Db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == input.Id, cancellationToken);
        if (existing is null)
            return errors;

        if (existing.Id == session.UserId)
        {
            if (!input.IsActive)
                errors.Add("You can't deactivate your own account.");
            if (existing.IsAdmin && !input.IsAdmin)
                errors.Add("You can't remove your own administrator rights.");
        }

        if (existing.IsAdmin && existing.IsActive && (!input.IsAdmin || !input.IsActive) && !await AnotherActiveAdminExistsAsync(existing.Id, cancellationToken))
            errors.Add("At least one active administrator is required.");

        return errors;
    }

    private Task<bool> AnotherActiveAdminExistsAsync(long exceptUserId, CancellationToken cancellationToken) =>
        Db.Users.AnyAsync(u => u.Id != exceptUserId && u.IsAdmin && u.IsActive, cancellationToken);

    protected override async Task<Error?> CheckCanDeleteAsync(User user, CancellationToken cancellationToken)
    {
        if (user.Id == session.UserId)
            return new Error("User.Self", "You can't deactivate your own account.");

        if (user.IsAdmin && user.IsActive && !await AnotherActiveAdminExistsAsync(user.Id, cancellationToken))
            return new Error("User.LastAdmin", "At least one active administrator is required.");

        return null;
    }

    // ---------------------------------------------------------------- saving

    protected override void ApplyFields(User user, UserInput input)
    {
        user.Username = input.Username!.Trim();
        user.FullName = input.FullName!.Trim();
        user.IsAdmin = input.IsAdmin;

        // input.Id, not user.Id: EF has already given a just-added entity a temporary id.
        if (input.Id == 0)
        {
            user.PasswordHash = hasher.Hash(input.TemporaryPassword!);
            user.MustChangePassword = true;
        }

        // Administrators can open everything, so they carry no permission rows.
        IEnumerable<PermissionLine> lines = input.IsAdmin ? [] : input.Permissions;

        SyncChildren(
            user.Permissions,
            lines,
            _ => 0,
            child => child.Id,
            (child, line) => child.ModuleId == line.ModuleId,
            (child, line) =>
            {
                child.ModuleId = line.ModuleId;
                child.AllowCreate = line.AllowCreate;
                child.AllowEdit = line.AllowEdit;
                child.AllowDelete = line.AllowDelete;
                child.AllowPrint = line.AllowPrint;
            },
            child => Db.UserPermissions.Remove(child));
    }

    protected override Task AfterSaveAsync(User user, UserInput input, CancellationToken cancellationToken)
    {
        // An administrator editing their own record sees the change at once; everyone else gets it at their next sign-in.
        if (user.Id == session.UserId)
        {
            session.Refresh(new AuthenticatedUser(
                user.Id,
                user.Username,
                user.FullName,
                user.IsAdmin,
                session.MustChangePassword,
                [.. user.Permissions.Select(p => new ModulePermission(p.ModuleId, p.AllowCreate, p.AllowEdit, p.AllowDelete, p.AllowPrint))]));
        }

        return Task.CompletedTask;
    }

    // ---------------------------------------------------------------- password and lockout

    public async Task<Result> ResetPasswordAsync(long userId, string temporaryPassword, CancellationToken cancellationToken = default)
    {
        if (!CurrentUser.Can(ModuleIds.Users, ModuleAction.Edit))
            return Result.Failure(Errors.Forbidden);

        var user = await Db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
            return Result.Failure(Errors.NotFound("User"));

        var errors = PasswordPolicy.Validate(temporaryPassword, user.Username);
        if (errors.Count > 0)
            return Result.Failure(Errors.Invalid(errors));

        user.PasswordHash = hasher.Hash(temporaryPassword);
        user.MustChangePassword = true;
        user.FailedLoginCount = 0;
        user.LockoutEndUtc = null;
        await Db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> UnlockAsync(long userId, CancellationToken cancellationToken = default)
    {
        if (!CurrentUser.Can(ModuleIds.Users, ModuleAction.Edit))
            return Result.Failure(Errors.Forbidden);

        var user = await Db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
            return Result.Failure(Errors.NotFound("User"));

        user.FailedLoginCount = 0;
        user.LockoutEndUtc = null;
        await Db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    // ---------------------------------------------------------------- lookups for the screen

    public async Task<Result<IReadOnlyList<PermissionGroup>>> GetPermissionCatalogAsync(CancellationToken cancellationToken = default)
    {
        if (!CurrentUser.Can(ModuleIds.Users, ModuleAction.View))
            return Result<IReadOnlyList<PermissionGroup>>.Failure(Errors.Forbidden);

        var modules = await Db.Modules
            .AsNoTracking()
            .Where(m => m.IsActive && m.ModuleType.IsActive && m.Id != ModuleIds.ChangePassword)
            .OrderBy(m => m.ModuleType.SortOrder).ThenBy(m => m.SortOrder)
            .Select(m => new
            {
                m.Id,
                m.ModuleName,
                m.HasCreate,
                m.HasEdit,
                m.HasDelete,
                m.HasPrint,
                TypeId = m.ModuleType.Id,
                TypeName = m.ModuleType.ModuleTypeName,
                m.ModuleType.IsAdmin,
            })
            .ToListAsync(cancellationToken);

        IReadOnlyList<PermissionGroup> groups =
        [
            .. modules.GroupBy(m => (m.TypeId, m.TypeName, m.IsAdmin)).Select(g => new PermissionGroup(
                g.Key.TypeName,
                g.Key.IsAdmin,
                [.. g.Select(m => new PermissionModule(m.Id, m.ModuleName, m.HasCreate, m.HasEdit, m.HasDelete, m.HasPrint))])),
        ];

        return Result<IReadOnlyList<PermissionGroup>>.Success(groups);
    }

    public async Task<Result<IReadOnlyList<LookupOption>>> GetUserOptionsAsync(CancellationToken cancellationToken = default)
    {
        if (!CurrentUser.Can(ModuleIds.Users, ModuleAction.View))
            return Result<IReadOnlyList<LookupOption>>.Failure(Errors.Forbidden);

        var users = await Db.Users.AsNoTracking().Where(u => u.IsActive).OrderBy(u => u.FullName)
            .Select(u => new { u.Id, u.FullName, u.Username })
            .ToListAsync(cancellationToken);

        IReadOnlyList<LookupOption> options = [.. users.Select(u => new LookupOption(u.Id, $"{u.FullName} ({u.Username})"))];
        return Result<IReadOnlyList<LookupOption>>.Success(options);
    }
}
