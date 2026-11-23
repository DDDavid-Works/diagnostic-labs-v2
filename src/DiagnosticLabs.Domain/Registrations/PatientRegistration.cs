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

    /// <summary>The maintained discount (PWD, Senior Citizen...) this one came from; null for a typed, one-off discount.</summary>
    public long? DiscountId { get; set; }

    public Discount? Discount { get; set; }

    public decimal? DiscountAmount { get; set; }

    public decimal? DiscountPercentage { get; set; }

    public decimal DiscountTotal { get; set; }

    /// <summary>
    /// The details of the maintained discount as they were when it was given, applied one after another (see <see cref="PatientRegistrationDiscountStep"/>).
    /// A registration without steps has a single typed discount (<see cref="DiscountAmount"/> or <see cref="DiscountPercentage"/>).
    /// </summary>
    public ICollection<PatientRegistrationDiscountStep> DiscountSteps { get; set; } = [];

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

/// <summary>
/// One detail of a maintained discount, copied onto the registration when the discount is given so that later changes to the discount
/// do not change what was already charged. The steps are applied in <see cref="Sequence"/> order, each to what is left after the one
/// before. Exactly one of <see cref="Amount"/> or <see cref="Percentage"/> is set.
/// </summary>
public class PatientRegistrationDiscountStep : SoftDeletableEntity
{
    public long PatientRegistrationId { get; set; }

    public PatientRegistration PatientRegistration { get; set; } = null!;

    public int Sequence { get; set; }

    public decimal? Amount { get; set; }

    public decimal? Percentage { get; set; }
}