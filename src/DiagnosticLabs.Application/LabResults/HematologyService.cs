using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Domain.Lab;
using DiagnosticLabs.Domain.Lab.Reports;

namespace DiagnosticLabs.Application.LabResults;

/// <summary>One line of a "test, normal values, result" table (Hematology, Clinical Chemistry): the normal values printed for it and the result.</summary>
public sealed record NormalResultEntry(string? NormalValue, string? Result);

/// <summary>The ten lines of the hematology table, in the order of the printed form.</summary>
public sealed record HematologyData
{
    public NormalResultEntry Hematocrit { get; init; } = new(null, null);

    public NormalResultEntry Hemoglobin { get; init; } = new(null, null);

    public NormalResultEntry WBCCount { get; init; } = new(null, null);

    public NormalResultEntry Segmenters { get; init; } = new(null, null);

    public NormalResultEntry Lymphocytes { get; init; } = new(null, null);

    public NormalResultEntry Eosinophils { get; init; } = new(null, null);

    public NormalResultEntry Monocytes { get; init; } = new(null, null);

    public NormalResultEntry Basophils { get; init; } = new(null, null);

    public NormalResultEntry Stab { get; init; } = new(null, null);

    public NormalResultEntry PlateletCount { get; init; } = new(null, null);
}

public sealed record HematologyDetails(long Id, LabResultHeader Header, HematologyData Data, byte[]? Photo, byte[] RowVersion) : IHasId;

public sealed record HematologyInput(long Id, LabResultHeader Header, HematologyData Data, byte[]? RowVersion) : ICrudInput;

public interface IHematologyService : ICrudService<LabResultListItem, HematologyDetails, HematologyInput>, ILabResultPrinting;

public sealed class HematologyService(IAppDbContext db, ICurrentUser currentUser, IClock clock)
    : LabResultService<HematologyInput, HematologyDetails, HematologyReport>(
        db, currentUser, clock, ModuleIds.Hematology, "Hematology", LabReportType.Hematology),
      IHematologyService
{
    public const int ValueMaxLength = 100;

    /// <summary>The lines of the table with the name each is printed and saved under.</summary>
    public static readonly (string Name, string Label)[] Tests =
    [
        ("Hematocrit", "Hematocrit"), ("Hemoglobin", "Hemoglobin"), ("WBCCount", "White Blood Cell Count"), ("Segmenters", "Segmenters"),
        ("Lymphocytes", "Lymphocytes"), ("Eosinophils", "Eosinophils"), ("Monocytes", "Monocytes"), ("Basophils", "Basophils"),
        ("Stab", "Stab"), ("PlateletCount", "Platelet Count"),
    ];

    protected override string Title => "Hematology";

    protected override ReportLayout Layout => ReportLayout.Hematology;

    protected override LabResultHeader HeaderOf(HematologyInput input) => input.Header;

    private static IEnumerable<(string Name, NormalResultEntry Entry)> Entries(HematologyData d) =>
    [
        ("Hematocrit", d.Hematocrit), ("Hemoglobin", d.Hemoglobin), ("WBCCount", d.WBCCount), ("Segmenters", d.Segmenters),
        ("Lymphocytes", d.Lymphocytes), ("Eosinophils", d.Eosinophils), ("Monocytes", d.Monocytes), ("Basophils", d.Basophils),
        ("Stab", d.Stab), ("PlateletCount", d.PlateletCount),
    ];

    protected override IEnumerable<string> ValidateDetail(HematologyInput input)
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

    protected override void ApplyDetail(HematologyReport e, HematologyInput input)
    {
        var d = input.Data;
        e.HematocritNValue = Clean(d.Hematocrit.NormalValue);
        e.HematocritResult = Clean(d.Hematocrit.Result);
        e.HemoglobinNValue = Clean(d.Hemoglobin.NormalValue);
        e.HemoglobinResult = Clean(d.Hemoglobin.Result);
        e.WBCCountNValue = Clean(d.WBCCount.NormalValue);
        e.WBCCountResult = Clean(d.WBCCount.Result);
        e.SegmentersNValue = Clean(d.Segmenters.NormalValue);
        e.SegmentersResult = Clean(d.Segmenters.Result);
        e.LymphocytesNValue = Clean(d.Lymphocytes.NormalValue);
        e.LymphocytesResult = Clean(d.Lymphocytes.Result);
        e.EosinophilsNValue = Clean(d.Eosinophils.NormalValue);
        e.EosinophilsResult = Clean(d.Eosinophils.Result);
        e.MonocytesNValue = Clean(d.Monocytes.NormalValue);
        e.MonocytesResult = Clean(d.Monocytes.Result);
        e.BasophilsNValue = Clean(d.Basophils.NormalValue);
        e.BasophilsResult = Clean(d.Basophils.Result);
        e.StabNValue = Clean(d.Stab.NormalValue);
        e.StabResult = Clean(d.Stab.Result);
        e.PlateletCountNValue = Clean(d.PlateletCount.NormalValue);
        e.PlateletCountResult = Clean(d.PlateletCount.Result);
    }

    private static HematologyData DataOf(HematologyReport e) => new()
    {
        Hematocrit = new(e.HematocritNValue, e.HematocritResult),
        Hemoglobin = new(e.HemoglobinNValue, e.HemoglobinResult),
        WBCCount = new(e.WBCCountNValue, e.WBCCountResult),
        Segmenters = new(e.SegmentersNValue, e.SegmentersResult),
        Lymphocytes = new(e.LymphocytesNValue, e.LymphocytesResult),
        Eosinophils = new(e.EosinophilsNValue, e.EosinophilsResult),
        Monocytes = new(e.MonocytesNValue, e.MonocytesResult),
        Basophils = new(e.BasophilsNValue, e.BasophilsResult),
        Stab = new(e.StabNValue, e.StabResult),
        PlateletCount = new(e.PlateletCountNValue, e.PlateletCountResult),
    };

    protected override HematologyDetails BuildDetails(LabReport report, LabResultHeader header, HematologyReport e) =>
        new(report.Id, header, DataOf(e), report.Photo?.Content, report.RowVersion);

    // The printed table places every value in its own cell, so the page design reads them by name ("HematocritNormalValue", "HematocritResult").
    protected override IReadOnlyList<PrintLine> ResultLines(HematologyReport detail) => [];

    protected override IReadOnlyList<PrintText> ResultTexts(HematologyReport detail) => [];

    protected override IReadOnlyDictionary<string, string?> PrintFields(LabReport report, HematologyReport e)
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
