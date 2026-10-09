using DiagnosticLabs.Domain.Common;
using DiagnosticLabs.Domain.Registrations;

namespace DiagnosticLabs.Domain.Billing;

public enum PaymentType
{
    /// <summary>Money received against the registration.</summary>
    Payment = 0,

    /// <summary>An amount charged to the registration (legacy <c>IsCharge</c>).</summary>
    Charge = 1,
}

public class Payment : SoftDeletableEntity
{
    public DateTime PaymentDate { get; set; }

    public long PatientRegistrationId { get; set; }

    public PatientRegistration PatientRegistration { get; set; } = null!;

    public decimal PaymentAmount { get; set; }

    public PaymentType Type { get; set; }
}
