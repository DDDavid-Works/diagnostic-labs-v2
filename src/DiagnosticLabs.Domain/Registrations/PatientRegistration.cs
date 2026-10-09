using DiagnosticLabs.Domain.Billing;
using DiagnosticLabs.Domain.Catalog;
using DiagnosticLabs.Domain.Common;
using DiagnosticLabs.Domain.Patients;

namespace DiagnosticLabs.Domain.Registrations;

public class PatientRegistration : SoftDeletableEntity
{
    public string RegistrationCode { get; set; } = string.Empty;

    public DateTime InputDate { get; set; }

    public long PatientId { get; set; }

    public Patient Patient { get; set; } = null!;

    /// <summary>Null for walk-ins that are not tied to a company.</summary>
    public long? CompanyId { get; set; }

    public Company? Company { get; set; }

    public long? PackageId { get; set; }

    public Package? Package { get; set; }

    public string BatchName { get; set; } = string.Empty;

    public decimal AmountDue { get; set; }

    public decimal? DiscountAmount { get; set; }

    public decimal? DiscountPercentage { get; set; }

    public decimal DiscountTotal { get; set; }

    public ICollection<PatientRegistrationService> Services { get; set; } = [];

    public ICollection<Payment> Payments { get; set; } = [];
}

public class PatientRegistrationService : SoftDeletableEntity
{
    public long PatientRegistrationId { get; set; }

    public PatientRegistration PatientRegistration { get; set; } = null!;

    public long ServiceId { get; set; }

    public Service Service { get; set; } = null!;

    public decimal Price { get; set; }
}
