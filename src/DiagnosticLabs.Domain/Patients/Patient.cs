using DiagnosticLabs.Domain.Common;

namespace DiagnosticLabs.Domain.Patients;

public class Patient : SoftDeletableEntity
{
    public string PatientCode { get; set; } = string.Empty;

    public string PatientName { get; set; } = string.Empty;

    /// <summary>Age is no longer stored; it is computed from this date.</summary>
    public DateOnly? DateOfBirth { get; set; }

    /// <summary>Renamed from the legacy <c>Gender</c> column so it matches the lab reports.</summary>
    public string? Sex { get; set; }

    public string? CivilStatus { get; set; }

    public string Address { get; set; } = string.Empty;

    public string? ContactNumbers { get; set; }
}
