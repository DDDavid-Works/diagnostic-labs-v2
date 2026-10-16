namespace DiagnosticLabs.Domain.Lab.Reports;

/// <summary>
/// The result of a form made in the Custom Lab Form designer. The patient header, signatories and registration link live on <see cref="LabReport"/> (type
/// <see cref="LabReportType.CustomForm"/>); the form and its design are found through <see cref="CustomFormVersionId"/>.
/// </summary>
public class CustomFormReport : LabReportDetail
{
    public long CustomFormVersionId { get; set; }

    public CustomFormVersion CustomFormVersion { get; set; } = null!;

    /// <summary>The entered values as JSON, keyed by the stable id of each row and textbox of the design.</summary>
    public string ValuesJson { get; set; } = "{}";
}
