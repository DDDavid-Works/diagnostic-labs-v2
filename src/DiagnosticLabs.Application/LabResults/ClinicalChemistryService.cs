using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Domain.Lab;
using DiagnosticLabs.Domain.Lab.Reports;

namespace DiagnosticLabs.Application.LabResults;

/// <summary>The nine lines of the clinical chemistry table, in the order of the printed form.</summary>
public sealed record ClinicalChemistryData
{
    public NormalResultEntry FBS { get; init; } = new(null, null);

    public NormalResultEntry TotalCholesterol { get; init; } = new(null, null);

    public NormalResultEntry Triglycerides { get; init; } = new(null, null);

    public NormalResultEntry HDL { get; init; } = new(null, null);

    public NormalResultEntry BUN { get; init; } = new(null, null);

    public NormalResultEntry Creatinine { get; init; } = new(null, null);

    public NormalResultEntry BloodUricAcid { get; init; } = new(null, null);

    public NormalResultEntry LDL { get; init; } = new(null, null);

    public NormalResultEntry ALTSGPT { get; init; } = new(null, null);
}

public sealed record ClinicalChemistryDetails(long Id, LabResultHeader Header, ClinicalChemistryData Data, byte[]? Photo, byte[] RowVersion) : IHasId;

public sealed record ClinicalChemistryInput(long Id, LabResultHeader Header, ClinicalChemistryData Data, byte[]? RowVersion) : ICrudInput;

public interface IClinicalChemistryService
    : ICrudService<LabResultListItem, ClinicalChemistryDetails, ClinicalChemistryInput>, ILabResultPrinting;

/// <summary>Clinical Chemistry: a table of nine tests, each with its normal values and a result. The form has no Remarks.</summary>
public sealed class ClinicalChemistryService(IAppDbContext db, ICurrentUser currentUser, IClock clock)
    : LabResultService<ClinicalChemistryInput, ClinicalChemistryDetails, ClinicalChemistryReport>(
        db, currentUser, clock, ModuleIds.ClinicalChemistry, "Clinical Chemistry", LabReportType.ClinicalChemistry),
      IClinicalChemistryService
{
    public const int ValueMaxLength = 100;

    /// <summary>The lines of the table with the name each is printed and saved under.</summary>
    public static readonly (string Name, string Label)[] Tests =
    [
        ("FBS", "FBS"), ("TotalCholesterol", "Total Cholesterol"), ("Triglycerides", "Triglycerides"), ("HDL", "HDL"), ("BUN", "BUN"),
        ("Creatinine", "Creatinine"), ("BloodUricAcid", "Blood Uric Acid"), ("LDL", "LDL"), ("ALTSGPT", "ALT/SGPT"),
    ];

    protected override string Title => "Clinical Chemistry";

    protected override ReportLayout Layout => ReportLayout.ClinicalChemistry;

    protected override LabResultHeader HeaderOf(ClinicalChemistryInput input) => input.Header;

    private static IEnumerable<(string Name, NormalResultEntry Entry)> Entries(ClinicalChemistryData d) =>
    [
        ("FBS", d.FBS), ("TotalCholesterol", d.TotalCholesterol), ("Triglycerides", d.Triglycerides), ("HDL", d.HDL), ("BUN", d.BUN),
        ("Creatinine", d.Creatinine), ("BloodUricAcid", d.BloodUricAcid), ("LDL", d.LDL), ("ALTSGPT", d.ALTSGPT),
    ];

    protected override IEnumerable<string> ValidateDetail(ClinicalChemistryInput input)
    {
        var errors = new List<string>();
        foreach (var (name, entry) in Entries(input.Data))
        {
            var label = Tests.First(t => t.Name == name).Label;
            Max(errors, entry.NormalValue, ValueMaxLength, $"{label} normal values");
            Max(errors, entry.Result, ValueMaxLength, $"{label} result");
        }

        return errors;
    }

    protected override void ApplyDetail(ClinicalChemistryReport e, ClinicalChemistryInput input)
    {
        var d = input.Data;
        e.FBSNValue = Clean(d.FBS.NormalValue);
        e.FBSResult = Clean(d.FBS.Result);
        e.TotalCholesterolNValue = Clean(d.TotalCholesterol.NormalValue);
        e.TotalCholesterolResult = Clean(d.TotalCholesterol.Result);
        e.TriglyceridesNValue = Clean(d.Triglycerides.NormalValue);
        e.TriglyceridesResult = Clean(d.Triglycerides.Result);
        e.HDLNValue = Clean(d.HDL.NormalValue);
        e.HDLResult = Clean(d.HDL.Result);
        e.BUNNValue = Clean(d.BUN.NormalValue);
        e.BUNResult = Clean(d.BUN.Result);
        e.CreatinineNValue = Clean(d.Creatinine.NormalValue);
        e.CreatinineResult = Clean(d.Creatinine.Result);
        e.BloodUricAcidNValue = Clean(d.BloodUricAcid.NormalValue);
        e.BloodUricAcidResult = Clean(d.BloodUricAcid.Result);
        e.LDLNValue = Clean(d.LDL.NormalValue);
        e.LDLResult = Clean(d.LDL.Result);
        e.ALTSGPTNValue = Clean(d.ALTSGPT.NormalValue);
        e.ALTSGPTResult = Clean(d.ALTSGPT.Result);
    }

    private static ClinicalChemistryData DataOf(ClinicalChemistryReport e) => new()
    {
        FBS = new(e.FBSNValue, e.FBSResult),
        TotalCholesterol = new(e.TotalCholesterolNValue, e.TotalCholesterolResult),
        Triglycerides = new(e.TriglyceridesNValue, e.TriglyceridesResult),
        HDL = new(e.HDLNValue, e.HDLResult),
        BUN = new(e.BUNNValue, e.BUNResult),
        Creatinine = new(e.CreatinineNValue, e.CreatinineResult),
        BloodUricAcid = new(e.BloodUricAcidNValue, e.BloodUricAcidResult),
        LDL = new(e.LDLNValue, e.LDLResult),
        ALTSGPT = new(e.ALTSGPTNValue, e.ALTSGPTResult),
    };

    protected override ClinicalChemistryDetails BuildDetails(LabReport report, LabResultHeader header, ClinicalChemistryReport e) =>
        new(report.Id, header, DataOf(e), report.Photo?.Content, report.RowVersion);

    // The printed table places every value in its own cell, so the page design reads them by name ("FBSNormalValue", "FBSResult").
    protected override IReadOnlyList<PrintLine> ResultLines(ClinicalChemistryReport detail) => [];

    protected override IReadOnlyList<PrintText> ResultTexts(ClinicalChemistryReport detail) => [];

    protected override IReadOnlyDictionary<string, string?> PrintFields(LabReport report, ClinicalChemistryReport e)
    {
        var fields = new Dictionary<string, string?>();
        foreach (var (name, entry) in Entries(DataOf(e)))
        {
            fields[name + "NormalValue"] = entry.NormalValue;
            fields[name + "Result"] = entry.Result;
        }

        return fields;
    }
}
