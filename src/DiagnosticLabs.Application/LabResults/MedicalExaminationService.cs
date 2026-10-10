using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Domain.Lab;
using DiagnosticLabs.Domain.Lab.Reports;

namespace DiagnosticLabs.Application.LabResults;

/// <summary>One line of the laboratory/X-ray table: normal ("N") or with findings ("F"), and a note.</summary>
public sealed record FindingEntry(string? Result, string? Remarks);

/// <summary>
/// What the Medical Examination Report holds beyond the shared result header: the laboratory and X-ray results, the classification
/// ("Fit", "Fit2", "Acceptable", "Unfit" or "Pending", as the old app saved it), the history, the assessment and who did and signed it.
/// In the app this form is called Annual Physical Exam Page 2.
/// </summary>
public sealed record MedicalExaminationData
{
    public string? ContactNo { get; init; }

    public string? CivilStatus { get; init; }

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

public sealed record MedicalExaminationDetails(long Id, LabResultHeader Header, MedicalExaminationData Data, byte[]? Photo, byte[] RowVersion) : IHasId;

public sealed record MedicalExaminationInput(long Id, LabResultHeader Header, MedicalExaminationData Data, byte[]? RowVersion) : ICrudInput;

public interface IMedicalExaminationService
    : ICrudService<LabResultListItem, MedicalExaminationDetails, MedicalExaminationInput>, ILabResultPrinting;

/// <summary>The Medical Examination Report, which the app calls Annual Physical Exam Page 2 (module 17); it is saved in the Medical Examination tables.</summary>
public sealed class MedicalExaminationService(IAppDbContext db, ICurrentUser currentUser, IClock clock)
    : LabResultService<MedicalExaminationInput, MedicalExaminationDetails, MedicalExaminationReport>(
        db, currentUser, clock, ModuleIds.AnnualPhysicalExamPage2, "Annual Physical Exam Page 2", LabReportType.MedicalExamination),
      IMedicalExaminationService
{
    public static readonly string[] Classifications = ["Fit", "Fit2", "Acceptable", "Unfit", "Pending"];

    protected override string Title => "Medical Examination Report";

    protected override ReportLayout Layout => ReportLayout.MedicalExamination;

    protected override LabResultHeader HeaderOf(MedicalExaminationInput input) => input.Header;

    private static IEnumerable<(string Label, FindingEntry Entry)> Findings(MedicalExaminationData d) =>
    [
        ("Chest X-Ray", d.ChestXray), ("CBC", d.CBC), ("Urinalysis", d.Urinalysis), ("Fecalysis", d.Fecalysis), ("HBsAg", d.HBsAg),
        ("Drug test (2 panel)", d.DrugTest2Panel), ("Drug test (4 panel)", d.DrugTest4Panel),
    ];

    protected override IEnumerable<string> ValidateDetail(MedicalExaminationInput input)
    {
        var d = input.Data;
        var errors = new List<string>();

        Max(errors, d.ContactNo, 100, "Contact number");
        Max(errors, d.CivilStatus, 50, "Civil status");
        foreach (var (label, entry) in Findings(d))
        {
            Max(errors, entry.Remarks, 100, $"{label} note");
            if (!string.IsNullOrWhiteSpace(entry.Result) && entry.Result.Trim() is not ("N" or "F"))
                errors.Add($"{label} must be normal (N) or with findings (F).");
        }

        if (!string.IsNullOrWhiteSpace(d.Classification) && !Classifications.Contains(d.Classification.Trim(), StringComparer.Ordinal))
            errors.Add($"Classification must be one of: {string.Join(", ", Classifications)}.");

        Max(errors, d.MedicalSurgicalHistory, 500, "Medical/surgical history");
        Max(errors, d.Assessment, 500, "Assessment");
        Max(errors, d.AssessmentDoneBy, 250, "Assessment done by");
        Max(errors, d.PhysicianName, 250, "Physician name");
        Max(errors, d.PhysicianLicense, 250, "Physician license");
        return errors;
    }

    protected override void ApplyDetail(MedicalExaminationReport e, MedicalExaminationInput input)
    {
        var d = input.Data;
        e.ContactNo = Clean(d.ContactNo);
        e.CivilStatus = Clean(d.CivilStatus);
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

    private static MedicalExaminationData DataOf(MedicalExaminationReport e) => new()
    {
        ContactNo = e.ContactNo,
        CivilStatus = e.CivilStatus,
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

    protected override MedicalExaminationDetails BuildDetails(LabReport report, LabResultHeader header, MedicalExaminationReport e) =>
        new(report.Id, header, DataOf(e), report.Photo?.Content, report.RowVersion);

    protected override IReadOnlyList<PrintLine> ResultLines(MedicalExaminationReport detail) => [];

    protected override IReadOnlyList<PrintText> ResultTexts(MedicalExaminationReport detail) =>
        [new("Medical/Surgical History", detail.MedicalSurgicalHistory), new("Assessment", detail.Assessment)];

    // The printed form places every value at its own spot (and has its own person box), so the page design reads them by name.
    protected override IReadOnlyDictionary<string, string?> PrintFields(LabReport report, MedicalExaminationReport e) => new Dictionary<string, string?>
    {
        ["Date"] = LocalTime.ToLocalDate(report.DateRequested).ToString("MM/dd/yyyy", System.Globalization.CultureInfo.InvariantCulture),
        ["Name"] = report.PatientName, ["Age"] = report.Age, ["Sex"] = report.Sex, ["CompanyName"] = report.CompanyOrPhysician,
        ["ContactNo"] = e.ContactNo, ["CivilStatus"] = e.CivilStatus,
        ["ChestXray"] = e.ChestXray, ["ChestXrayRemarks"] = e.ChestXrayRemarks,
        ["CBC"] = e.CBC, ["CBCRemarks"] = e.CBCRemarks,
        ["Urinalysis"] = e.Urinalysis, ["UrinalysisRemarks"] = e.UrinalysisRemarks,
        ["Fecalysis"] = e.Fecalysis, ["FecalysisRemarks"] = e.FecalysisRemarks,
        ["HBsAg"] = e.HBsAg, ["HBsAgRemarks"] = e.HBsAgRemarks,
        ["DrugTest2Panel"] = e.DrugTest2Panel, ["DrugTest2PanelRemarks"] = e.DrugTest2PanelRemarks,
        ["DrugTest4Panel"] = e.DrugTest4Panel, ["DrugTest4PanelRemarks"] = e.DrugTest4PanelRemarks,
        ["Classification"] = e.Classification,
        ["AssessmentDoneBy"] = e.AssessmentDoneBy, ["PhysicianName"] = e.PhysicianName, ["PhysicianLicense"] = e.PhysicianLicense,
    };
}