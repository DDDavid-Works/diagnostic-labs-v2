namespace DiagnosticLabs.Domain.Auditing;

public enum AuditAction
{
    Insert = 1,
    Update = 2,
    Delete = 3,
}

/// <summary>One row per changed entity per save: who, when, what, with old/new values as JSON.</summary>
public class AuditLog
{
    public long Id { get; set; }

    public string EntityName { get; set; } = string.Empty;

    public string EntityId { get; set; } = string.Empty;

    public AuditAction Action { get; set; }

    public long? ChangedByUserId { get; set; }

    public DateTime ChangedAtUtc { get; set; }

    /// <summary>JSON object of property to previous value (updates and deletes).</summary>
    public string? OldValues { get; set; }

    /// <summary>JSON object of property to new value (inserts and updates).</summary>
    public string? NewValues { get; set; }
}
