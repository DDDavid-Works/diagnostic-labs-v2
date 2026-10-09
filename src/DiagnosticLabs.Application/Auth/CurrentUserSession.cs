using DiagnosticLabs.Application.Abstractions;

namespace DiagnosticLabs.Application.Auth;

public sealed class CurrentUserSession : ICurrentUserSession
{
    private AuthenticatedUser? _user;

    public bool IsAuthenticated => _user is not null;

    public long? UserId => _user?.UserId;

    public string FullName => _user?.FullName ?? string.Empty;

    public bool IsAdmin => _user?.IsAdmin ?? false;

    public bool MustChangePassword => _user?.MustChangePassword ?? false;

    public bool CanAccess(int moduleId) => IsAdmin || GetPermission(moduleId) is not null;

    public bool Can(int moduleId, ModuleAction action)
    {
        if (!IsAuthenticated)
            return false;

        if (IsAdmin)
            return true;

        var permission = GetPermission(moduleId);
        return permission is not null && action switch
        {
            ModuleAction.View => true,
            ModuleAction.Create => permission.AllowCreate,
            ModuleAction.Edit => permission.AllowEdit,
            ModuleAction.Delete => permission.AllowDelete,
            ModuleAction.Print => permission.AllowPrint,
            _ => false,
        };
    }

    public ModulePermission? GetPermission(int moduleId) =>
        _user?.Permissions.FirstOrDefault(p => p.ModuleId == moduleId);

    public void SignIn(AuthenticatedUser user) => _user = user;

    public void Refresh(AuthenticatedUser user) => _user = user;

    public void MarkPasswordChanged() => _user = _user is null ? null : _user with { MustChangePassword = false };

    public void SignOut() => _user = null;
}
