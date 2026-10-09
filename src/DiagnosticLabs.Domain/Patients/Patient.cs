using DiagnosticLabs.Domain.Common;

namespace DiagnosticLabs.Domain.Patients;

public class Patient : SoftDeletableEntity
{
    public string PatientCode { get; set; } = string.Empty;

    public string PatientName { get; set; } = string.Empty;

    /// <summary>When known, the age is computed from this date and shown instead of <see cref="Age"/>.</summary>
    public DateOnly? DateOfBirth { get; set; }

    /// <summary>Free text ("35", "35 years old") used only when the birth date is unknown; cleared when a birth date is entered.</summary>
    public string? Age { get; set; }

    /// <summary>Renamed from the legacy <c>Gender</c> column so it matches the lab reports.</summary>
    public string? Sex { get; set; }

    public string? CivilStatus { get; set; }

    public string Address { get; set; } = string.Empty;

    public string? ContactNumbers { get; set; }
}
