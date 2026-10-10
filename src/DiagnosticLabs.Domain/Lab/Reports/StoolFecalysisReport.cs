namespace DiagnosticLabs.Domain.Lab.Reports;

/// <summary>Result fields of the Fecalysis form (the patient header lives on <see cref="LabReport"/>).</summary>
public class StoolFecalysisReport : LabReportDetail
{
    public string? Color { get; set; }

    public string? Consistency { get; set; }

    public string Others { get; set; } = string.Empty;

    /// <summary>White blood cells, per high power field (the unit is printed on the form).</summary>
    public string? Wbc { get; set; }

    /// <summary>Red blood cells, per high power field (the unit is printed on the form).</summary>
    public string? Rbc { get; set; }

    public string? Bacteria { get; set; }

    public string? YeastCells { get; set; }

    public string? FatGlobules { get; set; }

    public string? OvaParasite { get; set; }

    /// <summary>The second medical technologist who signs this form (the first is on the report header).</summary>
    public string? MedicalTechnologist2 { get; set; }

    public string? MedicalTechnologist2License { get; set; }

    /// <summary>
    /// The single free-text result of the old form. The screen no longer uses it; its text was moved into the report's Remarks
    /// (or into <see cref="Others"/> when Remarks already had text). The column stays until the client confirms nothing is missing.
    /// </summary>
    public string Result { get; set; } = string.Empty;
}
