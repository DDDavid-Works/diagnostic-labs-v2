namespace DiagnosticLabs.Domain.Common;

/// <summary>
/// Base for every business table. The audit columns are stamped by the persistence layer
/// (never by application code) and every change is also written to the AuditLog.
/// </summary>
public abstract class AuditableEntity
{
    public long Id { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    /// <summary>The user who created the row; <c>null</c> for system/seed rows.</summary>
    public long? CreatedByUserId { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public long? UpdatedByUserId { get; set; }

    /// <summary>Optimistic-concurrency token (SQL Server <c>rowversion</c>).</summary>
    public byte[] RowVersion { get; set; } = [];
}
