using DiagnosticLabs.Domain.Common;

namespace DiagnosticLabs.Domain.Identity;

public class User : ReferenceEntity
{
    public string Username { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// PBKDF2 hash. Rows migrated from the legacy app may still hold an unsalted SHA-1 hex digest
    /// until the user next signs in.
    /// </summary>
    public string PasswordHash { get; set; } = string.Empty;

    public bool IsAdmin { get; set; }

    public bool MustChangePassword { get; set; }

    public DateTime? LastLoginAtUtc { get; set; }

    public int FailedLoginCount { get; set; }

    public DateTime? LockoutEndUtc { get; set; }

    public ICollection<UserPermission> Permissions { get; set; } = [];
}
