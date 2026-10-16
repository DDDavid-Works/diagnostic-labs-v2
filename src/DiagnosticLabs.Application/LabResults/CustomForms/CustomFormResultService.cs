using System.Text.Json;
using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Domain.Lab;
using DiagnosticLabs.Domain.Lab.Reports;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Application.LabResults.CustomForms;

/// <summary>
/// Which custom form a <see cref="CustomFormResultService"/> works for. A form made in the Custom Lab Form designer is a module of its own (ids from 1000), but one
/// service class serves all of them, so the module is chosen when the service is made: whoever opens the scope sets <see cref="ModuleId"/> before asking
/// for the service (the screen's service runner does this).
/// </summary>
public sealed class CustomFormContext
{
    public int ModuleId { get; set; }
}

/// <summary>A form's design as a result sees it: the version the result was saved with (a new result gets the newest version).</summary>
public sealed record CustomFormDesign(long FormId, string Name, int ModuleId, long VersionId, int VersionNumber, bool FormIsActive, FormDefinition Definition);

/// <summary>How the entered values are keyed: a test row has a result and a reference value, a text box has one text; all by the row or box id.</summary>
public static class CustomFormValues
{
    public const int ResultMaxLength = 200;
    public const int ReferenceMaxLength = 100;
    public const int TextMaxLength = 2000;

    public static string ResultKey(Guid rowId) => rowId.ToString("N") + ".result";

    public static string ReferenceKey(Guid rowId) => rowId.ToString("N") + ".reference";

    public static string TextKey(Guid textboxId) => textboxId.ToString("N");

    /// <summary>Every key the design has a place for, with how long its value may be and what to call it in a message.</summary>
    public static IEnumerable<(string Key, int MaxLength, string Label)> Slots(FormDefinition design)
    {
        foreach (var group in design.Body.Groups)
        {
            foreach (var row in group.Rows.Where(r => r.Kind == RowKind.Test))
            {
                yield return (ResultKey(row.Id), ResultMaxLength, $"{row.Name} result");
                yield return (ReferenceKey(row.Id), ReferenceMaxLength, $"{row.Name} reference value");
            }
        }

        foreach (var box in design.Body.Textboxes)
            yield return (TextKey(box.Id), TextMaxLength, box.Label);
    }

    public static IReadOnlyDictionary<string, string?> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new Dictionary<string, string?>();

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string?>>(json) ?? [];
        }
        catch (JsonException)
        {
            return new Dictionary<string, string?>();
        }
    }
}

public sealed record CustomFormDetails(
    long Id, LabResultHeader Header, CustomFormDesign Design, IReadOnlyDictionary<string, string?> Values, byte[]? Photo, byte[] RowVersion) : IHasId;

public sealed record CustomFormInput(long Id, LabResultHeader Header, IReadOnlyDictionary<string, string?> Values, byte[]? RowVersion) : ICrudInput;

public interface ICustomFormResultService : ICrudService<LabResultListItem, CustomFormDetails, CustomFormInput>, ILabResultPrinting
{
    /// <summary>The newest design of the form, which a new result is made with. Needs View on the form's module.</summary>
    Task<Result<CustomFormDesign>> GetDesignAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// The results of a form made in the Custom Lab Form designer. Each result is a <see cref="LabReport"/> (patient snapshot, registration link, signatories, Remarks) plus a
/// <see cref="CustomFormReport"/> that points at the design version it was saved with and holds the values as JSON. Opening a saved result shows it with its own
/// version, so changing the design later never changes what an old result looks like.
/// </summary>
public sealed class CustomFormResultService
    : LabResultService<CustomFormInput, CustomFormDetails, CustomFormReport>, ICustomFormResultService
{
    private readonly int _moduleId;
    private readonly Dictionary<long, CustomFormDesign> _designs = [];
    private CustomFormDesign? _design; // the design the result being saved uses
    private string? _formName;

    public CustomFormResultService(IAppDbContext db, ICurrentUser currentUser, IClock clock, CustomFormContext context)
        : base(db, currentUser, clock, ModuleOf(context), "Result", LabReportType.CustomForm)
    {
        _moduleId = context.ModuleId;
    }

    private static int ModuleOf(CustomFormContext context) =>
        context.ModuleId >= ModuleIds.FirstCustomForm
            ? context.ModuleId
            : throw new InvalidOperationException("A custom form result service needs the module of its form.");

    protected override string Title => _formName ?? "Custom form";

    protected override ReportLayout Layout => ReportLayout.CustomForm;

    protected override string FooterText => ElectronicNote;

    // ---------------------------------------------------------------- only this form's results

    private IQueryable<LabReport> OfThisForm(IQueryable<LabReport> q) =>
        q.Where(r => Db.CustomFormReports.Any(c => c.LabReportId == r.Id && c.CustomFormVersion.CustomForm.ModuleId == _moduleId));

    protected override IQueryable<LabReport> ForListing(IQueryable<LabReport> q) => OfThisForm(base.ForListing(q));

    protected override IQueryable<LabReport> ForEditing(IQueryable<LabReport> q) => OfThisForm(base.ForEditing(q));

    protected override async Task<Error?> CheckCanDeleteAsync(LabReport entity, CancellationToken cancellationToken) =>
        await OfThisForm(Db.LabReports.Where(r => r.Id == entity.Id)).AnyAsync(cancellationToken) ? null : Errors.NotFound(EntityName);

    // ---------------------------------------------------------------- the design

    public async Task<Result<CustomFormDesign>> GetDesignAsync(CancellationToken cancellationToken = default)
    {
        if (!CurrentUser.Can(_moduleId, ModuleAction.View))
            return Result<CustomFormDesign>.Failure(Errors.Forbidden);

        var design = await LatestDesignAsync(cancellationToken);
        return design is null ? Result<CustomFormDesign>.Failure(Errors.NotFound("Form")) : Result<CustomFormDesign>.Success(design);
    }

    private async Task<CustomFormDesign?> LatestDesignAsync(CancellationToken cancellationToken) =>
        await LoadDesignAsync(Db.CustomFormVersions.Where(v => v.CustomForm.ModuleId == _moduleId).OrderByDescending(v => v.VersionNumber), cancellationToken);

    private async Task<CustomFormDesign?> DesignOfVersionAsync(long versionId, CancellationToken cancellationToken)
    {
        if (_designs.TryGetValue(versionId, out var known))
            return known;

        return await LoadDesignAsync(Db.CustomFormVersions.Where(v => v.Id == versionId && v.CustomForm.ModuleId == _moduleId), cancellationToken);
    }

    private async Task<CustomFormDesign?> LoadDesignAsync(IQueryable<CustomFormVersion> versions, CancellationToken cancellationToken)
    {
        var row = await versions.AsNoTracking()
            .Select(v => new { v.Id, v.VersionNumber, v.DefinitionJson, v.CustomFormId, v.CustomForm.Name, v.CustomForm.IsActive, v.CustomForm.ModuleId })
            .FirstOrDefaultAsync(cancellationToken);
        if (row is null)
            return null;

        var design = new CustomFormDesign(row.CustomFormId, row.Name, row.ModuleId, row.Id, row.VersionNumber, row.IsActive, FormDefinition.FromJson(row.DefinitionJson));
        _designs[row.Id] = design;
        _formName = row.Name;
        return design;
    }

    // ---------------------------------------------------------------- the lab result hooks

    protected override LabResultHeader HeaderOf(CustomFormInput input) => input.Header;

    // The values are checked against the design in ValidateAsync (they need the version the result belongs to).
    protected override IEnumerable<string> ValidateDetail(CustomFormInput input) => [];

    protected override async Task<IReadOnlyList<string>> ValidateAsync(CustomFormInput input, CancellationToken cancellationToken)
    {
        var errors = (await base.ValidateAsync(input, cancellationToken)).ToList();

        if (input.Id == 0)
        {
            _design = await LatestDesignAsync(cancellationToken);
            if (_design is null)
                errors.Add("This form no longer exists.");
            else if (!_design.FormIsActive)
                errors.Add("This form is switched off, so no new result can be made with it.");
        }
        else
        {
            var versionId = await Db.CustomFormReports.AsNoTracking().Where(c => c.LabReportId == input.Id).Select(c => (long?)c.CustomFormVersionId).FirstOrDefaultAsync(cancellationToken);
            _design = versionId is { } id ? await DesignOfVersionAsync(id, cancellationToken) : null;
            if (_design is null)
                errors.Add($"The result was not found.");
        }

        if (_design is not null)
        {
            foreach (var (key, max, label) in CustomFormValues.Slots(_design.Definition))
                Max(errors, input.Values.GetValueOrDefault(key), max, label);
        }

        return errors;
    }

    protected override void ApplyDetail(CustomFormReport detail, CustomFormInput input)
    {
        var design = _design!;
        if (detail.CustomFormVersionId == 0)
            detail.CustomFormVersionId = design.VersionId;

        // Only values the design has a place for are kept, empty ones are dropped, and the order follows the design.
        var values = new Dictionary<string, string>();
        foreach (var (key, _, _) in CustomFormValues.Slots(design.Definition))
        {
            if (Clean(input.Values.GetValueOrDefault(key)) is { } value)
                values[key] = value;
        }

        detail.ValuesJson = JsonSerializer.Serialize(values);
    }

    protected override async Task PrepareAsync(LabReport entity, CancellationToken cancellationToken)
    {
        await base.PrepareAsync(entity, cancellationToken);
        if (entity.Id == 0)
            return;

        var versionId = await Db.CustomFormReports.AsNoTracking().Where(c => c.LabReportId == entity.Id).Select(c => (long?)c.CustomFormVersionId).FirstOrDefaultAsync(cancellationToken);
        if (versionId is { } id)
            await DesignOfVersionAsync(id, cancellationToken);
    }

    public override async Task<Result<CustomFormDetails>> SaveAsync(CustomFormInput input, CancellationToken cancellationToken = default)
    {
        // The duplicate warning of the base service names the form, so its name is needed before anything else.
        _formName ??= await Db.CustomForms.AsNoTracking().Where(f => f.ModuleId == _moduleId).Select(f => f.Name).FirstOrDefaultAsync(cancellationToken);

        // A result of another form is not an existing result of this one.
        if (input.Id != 0 && CurrentUser.Can(_moduleId, ModuleAction.Edit)
            && !await OfThisForm(Db.LabReports.Where(r => r.Id == input.Id)).AnyAsync(cancellationToken))
            return Result<CustomFormDetails>.Failure(Errors.NotFound(EntityName));

        // The base service warns when the registration already has a result of the same report type, but every custom form has the same type. Only a result
        // of this very form is a repeat, so when there is none the warning is skipped.
        if (input.Id == 0 && input.Header is { RegistrationId: { } registrationId, ConfirmedDuplicate: false }
            && !await OfThisForm(Db.LabReports.Where(r => r.PatientRegistrationId == registrationId)).AnyAsync(cancellationToken))
            input = input with { Header = input.Header with { ConfirmedDuplicate = true } };

        return await base.SaveAsync(input, cancellationToken);
    }

    protected override CustomFormDetails BuildDetails(LabReport report, LabResultHeader header, CustomFormReport detail)
    {
        var design = _designs.GetValueOrDefault(detail.CustomFormVersionId) ?? _design
                     ?? throw new InvalidOperationException("The design of the result was not loaded.");
        return new CustomFormDetails(report.Id, header, design, CustomFormValues.Parse(detail.ValuesJson), report.Photo?.Content, report.RowVersion);
    }

    // ---------------------------------------------------------------- printing

    private CustomFormDesign? DesignOf(CustomFormReport detail) => _designs.GetValueOrDefault(detail.CustomFormVersionId);

    /// <summary>The printout of a saved result: the base report (letterhead, patient, signatories) plus the design and values the page is built from.</summary>
    public new async Task<Result<PrintableReport>> GetPrintableAsync(long id, CancellationToken cancellationToken = default)
    {
        var result = await base.GetPrintableAsync(id, cancellationToken);
        if (result.IsFailure)
            return result;

        var versionId = await Db.CustomFormReports.AsNoTracking().Where(c => c.LabReportId == id).Select(c => (long?)c.CustomFormVersionId).FirstOrDefaultAsync(cancellationToken);
        if (versionId is not { } v || !_designs.TryGetValue(v, out var design))
            return Result<PrintableReport>.Failure(Errors.NotFound(EntityName));

        var report = result.Value;
        var values = report.Fields ?? new Dictionary<string, string?>();
        var body = design.Definition.Body;

        // The header grid: each chosen patient detail with its label, in the cell the design put it in.
        PrintLine? Cell(HeaderField? field) => field switch
        {
            null => null,
            HeaderField.PatientCode => new PrintLine(FormDefinition.DisplayName(field.Value), PatientLine(report, "Patient Code")),
            HeaderField.PatientName => new PrintLine(FormDefinition.DisplayName(field.Value), PatientLine(report, "Patient Name")),
            HeaderField.Age => new PrintLine(FormDefinition.DisplayName(field.Value), PatientLine(report, "Age")),
            HeaderField.Sex => new PrintLine(FormDefinition.DisplayName(field.Value), PatientLine(report, "Sex")),
            HeaderField.Company => new PrintLine(FormDefinition.DisplayName(field.Value), PatientLine(report, "Company/Physician")),
            HeaderField.DateRequested => new PrintLine(FormDefinition.DisplayName(field.Value), PatientLine(report, "Date Requested")),
            _ => null,
        };

        var custom = new CustomFormPrint(
            [.. design.Definition.Header.Select(Cell)],
            [
                .. body.Groups.Select(g => new CustomFormPrintGroup(
                    g.Title,
                    g.ShowReference,
                    [
                        .. g.Rows.Select(r => r.Kind == RowKind.Section
                            ? new CustomFormPrintRow(true, r.Name, null, null)
                            : new CustomFormPrintRow(false, r.Name, values.GetValueOrDefault(CustomFormValues.ResultKey(r.Id)), values.GetValueOrDefault(CustomFormValues.ReferenceKey(r.Id)))),
                    ])),
            ],
            [.. body.Textboxes.Select(t => new CustomFormPrintText(t.Label, values.GetValueOrDefault(CustomFormValues.TextKey(t.Id))))],
            design.Definition.Footer.ShowRemarks,
            design.Definition.Footer.ShowLicenseNumber);

        // The Remarks box is only on the page when the design has it.
        var texts = design.Definition.Footer.ShowRemarks ? report.ResultTexts : [.. report.ResultTexts.Where(t => t.Label != "Remarks")];
        return Result<PrintableReport>.Success(report with { Custom = custom, ResultTexts = texts });
    }

    private static string? PatientLine(PrintableReport report, string label) => report.PatientLines.FirstOrDefault(l => l.Label == label)?.Value;

    protected override IReadOnlyList<PrintLine> ResultLines(CustomFormReport detail) => [];

    protected override IReadOnlyList<PrintText> ResultTexts(CustomFormReport detail) => [];

    protected override IReadOnlyList<PrintSignatory> Signatories(LabReport report, CustomFormReport detail)
    {
        var design = DesignOf(detail);
        if (design is null)
            return [];

        var footer = design.Definition.Footer;
        var technologists = 0;
        var list = new List<PrintSignatory>();
        foreach (var slot in footer.Signatories)
        {
            if (slot.Role == SignatoryRole.Pathologist)
            {
                list.Add(new PrintSignatory("Pathologist", report.Pathologist, footer.ShowLicenseNumber ? report.PathologistLicense : null));
            }
            else
            {
                var second = technologists++ > 0;
                list.Add(new PrintSignatory(
                    "Medical Technologist",
                    second ? report.MedicalTechnologist2 : report.MedicalTechnologist,
                    footer.ShowLicenseNumber ? (second ? report.MedicalTechnologist2License : report.MedicalTechnologistLicense) : null));
            }
        }

        return list;
    }

    protected override IReadOnlyDictionary<string, string?> PrintFields(LabReport report, CustomFormReport detail) =>
        new Dictionary<string, string?>(CustomFormValues.Parse(detail.ValuesJson));
}
