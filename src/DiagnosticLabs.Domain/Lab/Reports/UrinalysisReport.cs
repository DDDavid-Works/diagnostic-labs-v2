namespace DiagnosticLabs.Domain.Lab.Reports;

/// <summary>Result fields of the legacy <c>Urinalyses</c> table (the patient header lives on <see cref="LabReport"/>).</summary>
public class UrinalysisReport : LabReportDetail
{
    public string? Color { get; set; }

    public string? Appearance { get; set; }

    public string? Reaction { get; set; }

    public string? SPGravity { get; set; }

    public string? Albumin { get; set; }

    public string? Sugar { get; set; }

    public string? PusCells { get; set; }

    public string? RedCells { get; set; }

    public string? MucusThreads { get; set; }

    public string? EpithelialCells { get; set; }

    public string? AmorphousUratesPO4 { get; set; }

    public string? Bacteria { get; set; }

    public string? Casts { get; set; }

    public string? Crystals { get; set; }

    public string Others { get; set; } = string.Empty;

}
