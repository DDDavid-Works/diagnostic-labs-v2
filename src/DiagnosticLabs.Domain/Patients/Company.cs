using DiagnosticLabs.Domain.Common;

namespace DiagnosticLabs.Domain.Patients;

public class Company : ReferenceEntity
{
    public string CompanyName { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public string ContactNumbers { get; set; } = string.Empty;

    public string ContactPerson { get; set; } = string.Empty;

    /// <summary>System rows (e.g. WALK-IN) cannot be edited or removed by users.</summary>
    public bool IsSystem { get; set; }
}
