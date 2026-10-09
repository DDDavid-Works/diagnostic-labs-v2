using DiagnosticLabs.Domain.Common;

namespace DiagnosticLabs.Domain.Identity;

/// <summary>
/// Having a row for a module grants view access to it. The Allow flags add actions on top
/// (the legacy ViewOnly flag is gone: it was just "no Allow flags").
/// </summary>
public class UserPermission : AuditableEntity
{
    public long UserId { get; set; }

    public int ModuleId { get; set; }

    public Module Module { get; set; } = null!;

    public bool AllowCreate { get; set; }

    public bool AllowEdit { get; set; }

    public bool AllowDelete { get; set; }

    public bool AllowPrint { get; set; }
}
