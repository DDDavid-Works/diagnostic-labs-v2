using DiagnosticLabs.Domain.Common;
using DiagnosticLabs.Domain.Patients;

namespace DiagnosticLabs.Domain.Billing;

public enum StatementMode
{
    /// <summary>What a company has been charged: the registrations charged to it (one batch of it, or all), with no payments.</summary>
    Company = 0,

    /// <summary>What one patient has been billed, what they paid and what is left.</summary>
    Patient = 1,
}

/// <summary>
/// A Statement of Account as it was handed out. The lines are a copy of the registrations' amounts at the moment it was generated, so a reprint
/// always shows what the client received, whatever has been paid, discounted or corrected since. A statement is never edited: it is voided and,
/// when the figures changed, generated again as a new statement (<see cref="RegeneratedFromId"/> points back to the voided one).
/// </summary>
public class StatementOfAccount : AuditableEntity
{
    /// <summary>For example <c>SOA-20261012-0001</c>; the counter restarts every day.</summary>
    public string StatementNumber { get; set; } = string.Empty;

    public StatementMode Mode { get; set; }

    /// <summary>Company mode: the company charged. Kept with its name as it was, in case the company is renamed later.</summary>
    public long? CompanyId { get; set; }

    public Company? Company { get; set; }

    public string? CompanyName { get; set; }

    /// <summary>Company mode: the batch, or null for the whole company.</summary>
    public string? BatchName { get; set; }

    /// <summary>Company mode: the person the statement is addressed to (typed when it was generated).</summary>
    public string? Attention { get; set; }

    /// <summary>Patient mode: the patient, with the name and code as they were.</summary>
    public long? PatientId { get; set; }

    public Patient? Patient { get; set; }

    public string? PatientName { get; set; }

    public string? PatientCode { get; set; }

    /// <summary>The optional range of registration days (both included); both null means every date.</summary>
    public DateOnly? DateFrom { get; set; }

    public DateOnly? DateTo { get; set; }

    public decimal GrossTotal { get; set; }

    public decimal DiscountTotal { get; set; }

    public decimal NetTotal { get; set; }

    /// <summary>Patient mode only (a company statement is paid by the company later and shows no payments).</summary>
    public decimal PaidTotal { get; set; }

    public decimal BalanceTotal { get; set; }

    public bool IsVoid { get; set; }

    public DateTime? VoidedAtUtc { get; set; }

    public long? VoidedByUserId { get; set; }

    /// <summary>The voided statement this one replaces, when it was made with Regenerate.</summary>
    public long? RegeneratedFromId { get; set; }

    public ICollection<StatementOfAccountLine> Lines { get; set; } = [];
}

/// <summary>One registration on a statement. Plain on purpose (no audit row per line): the statement carries the audit.</summary>
public class StatementOfAccountLine
{
    public long Id { get; set; }

    public long StatementOfAccountId { get; set; }

    public StatementOfAccount StatementOfAccount { get; set; } = null!;

    /// <summary>The order the lines are printed in.</summary>
    public int Sequence { get; set; }

    public long PatientRegistrationId { get; set; }

    public string RegistrationCode { get; set; } = string.Empty;

    public string PatientName { get; set; } = string.Empty;

    public DateOnly RegistrationDate { get; set; }

    public decimal Gross { get; set; }

    public decimal Discount { get; set; }

    public decimal Net { get; set; }

    public decimal Paid { get; set; }

    public decimal Balance { get; set; }
}
