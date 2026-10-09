namespace DiagnosticLabs.Domain.Common;

/// <summary>
/// A list entry that can be switched off (no longer offered) but is never "deleted":
/// services, packages, items, companies, users and similar reference data.
/// </summary>
public abstract class ReferenceEntity : AuditableEntity
{
    public bool IsActive { get; set; } = true;
}
