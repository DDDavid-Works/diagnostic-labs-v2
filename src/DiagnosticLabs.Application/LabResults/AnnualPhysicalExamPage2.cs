using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Domain.Lab;
using DiagnosticLabs.Domain.Lab.Reports;

namespace DiagnosticLabs.Application.LabResults;

/// <summary>One line of the laboratory/X-ray table: normal ("N") or with findings ("F"), and a note.</summary>
public sealed record FindingEntry(string? Result, string? Remarks);

/// <summary>
/// Page 2 of the Annual Physical Exam (the Medical Examination Report): the laboratory and X-ray results, the classification
/// ("Fit", "Fit2", "Acceptable", "Unfit" or "Pending", as the old app saved it), the history, the assessment and who did and signed it.
/// It is saved in the Medical Examination tables, next to the first page of the same result; its Remarks are the result's Remarks and
/// the contact number and civil status are the first page's.
/// </summary>
public sealed record MedicalExaminationData
{
    public FindingEntry ChestXray { get; init; } = new(null, null);

    public FindingEntry CBC { get; init; } = new(null, null);

    public FindingEntry Urinalysis { get; init; } = new(null, null);

    public FindingEntry Fecalysis { get; init; } = new(null, null);

    /// <summary>"N" is non-reactive, "F" is reactive.</summary>
    public FindingEntry HBsAg { get; init; } = new(null, null);

    /// <summary>"N" is negative, "F" is positive.</summary>
    public FindingEntry DrugTest2Panel { get; init; } = new(null, null);

    /// <summary>"N" is negative, "F" is positive.</summary>
    public FindingEntry DrugTest4Panel { get; init; } = new(null, null);

    public string? Classification { get; init; }

    public string? MedicalSurgicalHistory { get; init; }

    public string? Assessment { get; init; }

    public string? AssessmentDoneBy { get; init; }

    public string? PhysicianName { get; init; }

    public string? PhysicianLicense { get; init; }
}

/// <summary>How Page 2 of the Annual Physical Exam is checked, saved, read back and printed.</summary>
internal static class AnnualPhysicalExamPage2
{
    public const string Title = "Medical Examination Report";

    private static IEnumerable<(string Label, FindingEntry Entry)> Findings(MedicalExaminationData d) =>
    [
        ("Chest X-Ray", d.ChestXray), ("CBC", d.CBC), ("Urinalysis", d.Urinalysis), ("Fecalysis", d.Fecalysis), ("HBsAg", d.HBsAg),
        ("Drug test (2 panel)", d.DrugTest2Panel), ("Drug test (4 panel)", d.DrugTest4Panel),
    ];

    public static IEnumerable<string> Validate(MedicalExaminationData d)
    {
        var errors = new List<string>();

        foreach (var (label, entry) in Findings(d))
        {
            Max(errors, entry.Remarks, 100, $"{label} note");
            if (!string.IsNullOrWhiteSpace(entry.Result) && entry.Result.Trim() is not ("N" or "F"))
                errors.Add($"{label} must be normal (N) or with findings (F).");
        }

        if (!string.IsNullOrWhiteSpace(d.Classification) && !AnnualPhysicalExamService.Classifications.Contains(d.Classification.Trim(), StringComparer.Ordinal))
            errors.Add($"Classification must be one of: {string.Join(", ", AnnualPhysicalExamService.Classifications)}.");

        Max(errors, d.MedicalSurgicalHistory, 500, "Medical/surgical history");
        Max(errors, d.Assessment, 500, "Assessment");
        Max(errors, d.AssessmentDoneBy, 250, "Assessment done by");
        Max(errors, d.PhysicianName, 250, "Physician name");
        Max(errors, d.PhysicianLicense, 250, "Physician license");
        return errors;
    }

    private static void Max(List<string> errors, string? value, int length, string label)
    {
        if ((value?.Trim().Length ?? 0) > length)
            errors.Add($"{label} can not be longer than {length} characters.");
    }

    // The page's own ContactNo and CivilStatus columns are hidden (docs/HIDDEN-FIELDS.md): the first page's are used, and saving leaves them alone.
    public static void Apply(MedicalExaminationReport e, MedicalExaminationData d)
    {
        e.ChestXray = Clean(d.ChestXray.Result);
        e.ChestXrayRemarks = Clean(d.ChestXray.Remarks);
        e.CBC = Clean(d.CBC.Result);
        e.CBCRemarks = Clean(d.CBC.Remarks);
        e.Urinalysis = Clean(d.Urinalysis.Result);
        e.UrinalysisRemarks = Clean(d.Urinalysis.Remarks);
        e.Fecalysis = Clean(d.Fecalysis.Result);
        e.FecalysisRemarks = Clean(d.Fecalysis.Remarks);
        e.HBsAg = Clean(d.HBsAg.Result);
        e.HBsAgRemarks = Clean(d.HBsAg.Remarks);
        e.DrugTest2Panel = Clean(d.DrugTest2Panel.Result);
        e.DrugTest2PanelRemarks = Clean(d.DrugTest2Panel.Remarks);
        e.DrugTest4Panel = Clean(d.DrugTest4Panel.Result);
        e.DrugTest4PanelRemarks = Clean(d.DrugTest4Panel.Remarks);
        e.Classification = Clean(d.Classification);
        e.MedicalSurgicalHistory = Clean(d.MedicalSurgicalHistory);
        e.Assessment = Clean(d.Assessment);
        e.AssessmentDoneBy = Clean(d.AssessmentDoneBy);
        e.PhysicianName = Clean(d.PhysicianName);
        e.PhysicianLicense = Clean(d.PhysicianLicense);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static MedicalExaminationData DataOf(MedicalExaminationReport e) => new()
    {
        ChestXray = new(e.ChestXray, e.ChestXrayRemarks),
        CBC = new(e.CBC, e.CBCRemarks),
        Urinalysis = new(e.Urinalysis, e.UrinalysisRemarks),
        Fecalysis = new(e.Fecalysis, e.FecalysisRemarks),
        HBsAg = new(e.HBsAg, e.HBsAgRemarks),
        DrugTest2Panel = new(e.DrugTest2Panel, e.DrugTest2PanelRemarks),
        DrugTest4Panel = new(e.DrugTest4Panel, e.DrugTest4PanelRemarks),
        Classification = e.Classification,
        MedicalSurgicalHistory = e.MedicalSurgicalHistory,
        Assessment = e.Assessment,
        AssessmentDoneBy = e.AssessmentDoneBy,
        PhysicianName = e.PhysicianName,
        PhysicianLicense = e.PhysicianLicense,
    };

    /// <summary>The printed page 2: its own person box (with the first page's contact number and civil status), the three text boxes and the physician.</summary>
    public static PrintableReport Printable(
        LabReport report, AnnualPhysicalExamReport page1, MedicalExaminationReport e, ReportLetterhead letterhead, IReadOnlyList<PrintLine> patient) => new(
        ReportLayout.MedicalExamination,
        Title,
        letterhead,
        patient,
        [],
        [new("Medical/Surgical History", e.MedicalSurgicalHistory), new("Assessment", e.Assessment), new("Remarks", report.Remarks)],
        [],
        string.Empty,
        new Dictionary<string, string?>
        {
            ["Date"] = LocalTime.ToLocalDate(report.DateRequested).ToString("MM/dd/yyyy", System.Globalization.CultureInfo.InvariantCulture),
            ["Name"] = report.PatientName, ["Age"] = report.Age, ["Sex"] = report.Sex, ["CompanyName"] = report.CompanyOrPhysician,
            ["ContactNo"] = page1.ContactNo, ["CivilStatus"] = page1.CivilStatus,
            ["ChestXray"] = e.ChestXray, ["ChestXrayRemarks"] = e.ChestXrayRemarks,
            ["CBC"] = e.CBC, ["CBCRemarks"] = e.CBCRemarks,
            ["Urinalysis"] = e.Urinalysis, ["UrinalysisRemarks"] = e.UrinalysisRemarks,
            ["Fecalysis"] = e.Fecalysis, ["FecalysisRemarks"] = e.FecalysisRemarks,
            ["HBsAg"] = e.HBsAg, ["HBsAgRemarks"] = e.HBsAgRemarks,
            ["DrugTest2Panel"] = e.DrugTest2Panel, ["DrugTest2PanelRemarks"] = e.DrugTest2PanelRemarks,
            ["DrugTest4Panel"] = e.DrugTest4Panel, ["DrugTest4PanelRemarks"] = e.DrugTest4PanelRemarks,
            ["Classification"] = e.Classification,
            ["AssessmentDoneBy"] = e.AssessmentDoneBy, ["PhysicianName"] = e.PhysicianName, ["PhysicianLicense"] = e.PhysicianLicense,
        });
}
