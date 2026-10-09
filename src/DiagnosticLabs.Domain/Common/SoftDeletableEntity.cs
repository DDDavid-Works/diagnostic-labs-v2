namespace DiagnosticLabs.Domain.Common;

public interface ISoftDeletable
{
    bool IsDeleted { get; set; }

    DateTime? DeletedAtUtc { get; set; }

    long? DeletedByUserId { get; set; }
}

/// <summary>
/// A record (patient, registration, payment, lab report...) that is removed by flagging it,
/// so history is preserved. Removed rows are hidden by a global query filter.
/// </summary>
public abstract class SoftDeletableEntity : AuditableEntity, ISoftDeletable
{
    public bool IsDeleted { get; set; }

    public DateTime? DeletedAtUtc { get; set; }

    public long? DeletedByUserId { get; set; }
}
