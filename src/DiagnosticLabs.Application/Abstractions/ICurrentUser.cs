namespace DiagnosticLabs.Application.Abstractions;

public enum ModuleAction
{
    View,
    Create,
    Edit,
    Delete,
    Print,
}

/// <summary>Replaces the legacy static <c>Globals</c>: who is signed in and what they may do.</summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    /// <summary>The signed-in user's id, or <c>null</c> when nobody is signed in (e.g. seeding).</summary>
    long? UserId { get; }

    string FullName { get; }

    bool IsAdmin { get; }

    bool CanAccess(int moduleId);

    /// <summary>Whether the user may perform <paramref name="action"/> in the module. Administrators may do everything.</summary>
    bool Can(int moduleId, ModuleAction action);

    ModulePermission? GetPermission(int moduleId);
}

public interface ICurrentUserSession : ICurrentUser
{
    void SignIn(AuthenticatedUser user);

    void SignOut();
}

/// <summary>Access to a module implies view; the flags add actions.</summary>
public sealed record ModulePermission(
    int ModuleId,
    bool AllowCreate,
    bool AllowEdit,
    bool AllowDelete,
    bool AllowPrint);

public sealed record AuthenticatedUser(
    long UserId,
    string Username,
    string FullName,
    bool IsAdmin,
    bool MustChangePassword,
    IReadOnlyList<ModulePermission> Permissions);
