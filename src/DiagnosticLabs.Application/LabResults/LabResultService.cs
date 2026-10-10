using System.Runtime.CompilerServices;
using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Domain.Lab;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Application.LabResults;

/// <summary>
/// The part of a lab result every type shares: who it is for (a snapshot), when it was requested, the remarks and the two signatories.
/// <see cref="RegistrationId"/> is optional: a result can be printed for someone who is not registered at all.
/// </summary>
public sealed record LabResultHeader(
    long? RegistrationId,
    string? RegistrationCode,
    string? PatientCode,
    string? PatientName,
    string? Age,
    string? Sex,
    string? CompanyOrPhysician,
    DateOnly DateRequested,
    string? Remarks,
    string? MedicalTechnologist,
    string? Pathologist,
    bool ConfirmedDuplicate = false,
    long? PatientId = null);

public sealed record LabResultListItem(
    long Id, DateOnly Date, string PatientName, string? RegistrationCode, string? CompanyOrPhysician) : IHasId;

public sealed record LabResultSearch(
    string? Text,
    DateOnly? Date = null,
    int Page = 1,
    int PageSize = Paging.DefaultPageSize,
    bool IncludeInactive = false) : CrudSearch(Text, Page, PageSize, IncludeInactive);

public interface ILabResultPrinting
{
    /// <summary>Everything the printout of a saved result shows.</summary>
    Task<Result<PrintableReport>> GetPrintableAsync(long id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Shared workflow of the lab result screens. A concrete type (stool, urinalysis...) only says which fields its detail table has,
/// how they validate and how they appear on paper; the header, registration link, duplicate warning, search and printing are here.
/// </summary>
public abstract class LabResultService<TInput, TDetails, TDetail>(
    IAppDbContext db,
    ICurrentUser currentUser,
    IClock clock,
    int moduleId,
    string entityName,
    LabReportType reportType)
    : CrudService<LabReport, LabResultListItem, TDetails, TInput>(db, currentUser, moduleId, entityName), ILabResultPrinting
    where TInput : ICrudInput
    where TDetails : IHasId
    where TDetail : LabReportDetail, new()
{
    public const string DuplicateCode = "LabResult.Duplicate";
    public const string FooterNote = "** COMPUTER GENERATED ** This is validated and original report **";

    private readonly int _module = moduleId;
    private readonly ConditionalWeakTable<LabReport, TDetail> _details = [];
    private Domain.Registrations.PatientRegistration? _registration;
    private Domain.Patients.Patient? _patient;

    protected abstract LabResultHeader HeaderOf(TInput input);

    protected abstract void ApplyDetail(TDetail detail, TInput input);

    protected abstract IEnumerable<string> ValidateDetail(TInput input);

    protected abstract TDetails BuildDetails(LabReport report, LabResultHeader header, TDetail detail);

    protected abstract string Title { get; }

    protected abstract ReportLayout Layout { get; }

    protected abstract IReadOnlyList<PrintLine> ResultLines(TDetail detail);

    protected abstract IReadOnlyList<PrintText> ResultTexts(TDetail detail);

    protected override DbSet<LabReport> Set => Db.LabReports;

    protected override IQueryable<LabReport> ForListing(IQueryable<LabReport> q) =>
        q.Include(r => r.PatientRegistration).Where(r => r.ReportType == reportType);

    protected override IQueryable<LabReport> ForEditing(IQueryable<LabReport> q) =>
        q.Include(r => r.PatientRegistration).Where(r => r.ReportType == reportType);

    // Soft-deleted results are already hidden by the global query filter.
    protected override IQueryable<LabReport> ApplyActiveFilter(IQueryable<LabReport> q, bool includeInactive) => q.Where(r => r.ReportType == reportType);

    protected override IQueryable<LabReport> Matches(IQueryable<LabReport> q, string word) =>
        q.Where(r => r.PatientName.Contains(word)
                     || r.PatientCode.Contains(word)
                     || (r.PatientRegistration != null && r.PatientRegistration.RegistrationCode.Contains(word)));

    protected override IOrderedQueryable<LabReport> Order(IQueryable<LabReport> q) =>
        q.OrderByDescending(r => r.DateRequested).ThenByDescending(r => r.Id);

    protected override IQueryable<LabReport> Filter(IQueryable<LabReport> q, CrudSearch search)
    {
        if (search is LabResultSearch { Date: { } date })
        {
            var (from, to) = LocalTime.DayBoundsUtc(date);
            q = q.Where(r => r.DateRequested >= from && r.DateRequested < to);
        }

        return q;
    }

    protected override void Remove(LabReport entity) => Set.Remove(entity);

    protected override LabResultListItem ToListItem(LabReport r) =>
        new(r.Id, LocalTime.ToLocalDate(r.DateRequested), r.PatientName, r.PatientRegistration?.RegistrationCode, r.CompanyOrPhysician);

    protected override TDetails ToDetails(LabReport r)
    {
        var detail = _details.TryGetValue(r, out var loaded) ? loaded : new TDetail();
        var header = new LabResultHeader(
            r.PatientRegistrationId, r.PatientRegistration?.RegistrationCode, r.PatientCode, r.PatientName, r.Age, r.Sex,
            r.CompanyOrPhysician, LocalTime.ToLocalDate(r.DateRequested), r.Remarks, r.MedicalTechnologist, r.Pathologist,
            PatientId: r.PatientId);
        return BuildDetails(r, header, detail);
    }

    protected override async Task PrepareAsync(LabReport entity, CancellationToken cancellationToken)
    {
        if (entity.Id == 0 || _details.TryGetValue(entity, out _))
            return;

        var detail = await Db.Set<TDetail>().FirstOrDefaultAsync(d => d.LabReportId == entity.Id, cancellationToken);
        if (detail is not null)
            _details.Add(entity, detail);
    }

    // ---------------------------------------------------------------- validation

    protected override IReadOnlyList<string> Validate(TInput input)
    {
        var header = HeaderOf(input);
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(header.PatientName))
            errors.Add("Patient name is required.");
        else if (header.PatientName.Trim().Length > 200)
            errors.Add("Patient name can not be longer than 200 characters.");

        Max(errors, header.PatientCode, 200, "Patient code");
        Max(errors, header.Age, 50, "Age");
        Max(errors, header.Sex, 20, "Sex");
        Max(errors, header.CompanyOrPhysician, 200, "Company/Physician");
        Max(errors, header.Remarks, 500, "Remarks");
        Max(errors, header.MedicalTechnologist, 100, "Medical technologist");
        Max(errors, header.Pathologist, 100, "Pathologist");

        errors.AddRange(ValidateDetail(input));
        return errors;
    }

    protected static void Max(List<string> errors, string? value, int length, string label)
    {
        if ((value?.Trim().Length ?? 0) > length)
            errors.Add($"{label} can not be longer than {length} characters.");
    }

    protected override async Task<IReadOnlyList<string>> ValidateAsync(TInput input, CancellationToken cancellationToken)
    {
        var header = HeaderOf(input);
        _registration = null;
        _patient = null;

        if (header.RegistrationId is { } registrationId)
        {
            _registration = await Db.PatientRegistrations.Include(r => r.Patient).FirstOrDefaultAsync(r => r.Id == registrationId, cancellationToken);
            return _registration is null ? ["The registration no longer exists."] : [];
        }

        // Without a registration a result can still be linked to a patient picked from the patient list.
        if (header.PatientId is { } patientId)
        {
            _patient = await Db.Patients.FirstOrDefaultAsync(p => p.Id == patientId, cancellationToken);
            return _patient is null ? ["The patient no longer exists."] : [];
        }

        return [];
    }

    /// <summary>
    /// A registration can have the same kind of result more than once (a repeat test), but that is usually a slip,
    /// so a second one is refused with <see cref="DuplicateCode"/> until the caller confirms.
    /// </summary>
    public override async Task<Result<TDetails>> SaveAsync(TInput input, CancellationToken cancellationToken = default)
    {
        var header = HeaderOf(input);
        if (input.Id == 0 && header is { RegistrationId: { } registrationId, ConfirmedDuplicate: false }
            && CurrentUser.Can(_module, ModuleAction.Create)
            && await Db.LabReports.AnyAsync(r => r.PatientRegistrationId == registrationId && r.ReportType == reportType, cancellationToken))
        {
            return Result<TDetails>.Failure(new Error(DuplicateCode, $"This registration already has a {Title} result. Save another one anyway?"));
        }

        return await base.SaveAsync(input, cancellationToken);
    }

    // ---------------------------------------------------------------- saving

    protected override Task<LabReport> CreateAsync(TInput input, CancellationToken cancellationToken)
    {
        var report = new LabReport { ReportType = reportType };
        var detail = new TDetail { LabReport = report };
        Db.Set<TDetail>().Add(detail);
        _details.Add(report, detail);
        return Task.FromResult(report);
    }

    protected override void Apply(LabReport report, TInput input)
    {
        var header = HeaderOf(input);

        report.PatientRegistration = _registration;
        report.PatientRegistrationId = _registration?.Id;
        report.PatientId = _registration?.PatientId ?? _patient?.Id;
        report.PatientCode = Clean(header.PatientCode) ?? _registration?.Patient.PatientCode ?? string.Empty;
        report.PatientName = header.PatientName!.Trim();
        report.Age = Clean(header.Age);
        report.Sex = Clean(header.Sex);
        report.CompanyOrPhysician = Clean(header.CompanyOrPhysician);

        // Keep the time of day of a result whose date was not changed; a new or moved one gets "now" on that date.
        if (report.DateRequested == default || LocalTime.ToLocalDate(report.DateRequested) != header.DateRequested)
            report.DateRequested = LocalTime.ToUtc(header.DateRequested, clock.UtcNow.ToLocalTime().TimeOfDay);

        report.Remarks = Clean(header.Remarks);
        report.MedicalTechnologist = Clean(header.MedicalTechnologist);
        report.Pathologist = Clean(header.Pathologist);

        if (_details.TryGetValue(report, out var detail))
            ApplyDetail(detail, input);
    }

    // ---------------------------------------------------------------- printing

    public async Task<Result<PrintableReport>> GetPrintableAsync(long id, CancellationToken cancellationToken = default)
    {
        if (!CurrentUser.Can(_module, ModuleAction.Print))
            return Result<PrintableReport>.Failure(Errors.Forbidden);

        var report = await ForEditing(Set.AsNoTracking()).FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (report is null)
            return Result<PrintableReport>.Failure(Errors.NotFound(EntityName));

        await PrepareAsync(report, cancellationToken);
        var detail = _details.TryGetValue(report, out var loaded) ? loaded : new TDetail();

        var setup = await Db.CompanySetups.AsNoTracking().OrderByDescending(c => c.UpdatedAtUtc).ThenByDescending(c => c.Id).FirstOrDefaultAsync(cancellationToken);
        var letterhead = new ReportLetterhead(
            setup?.CompanyName ?? string.Empty, setup?.SubCompanyName, setup?.Address, setup?.ContactNumbers, setup?.Email, setup?.Logo);

        PrintLine[] patient =
        [
            new("Patient Code", report.PatientCode),
            new("Patient Name", report.PatientName),
            new("Company/Physician", report.CompanyOrPhysician),
            new("Age", report.Age),
            new("Sex", report.Sex),
            new("Date Requested", LocalTime.ToLocalDate(report.DateRequested).ToString("d", System.Globalization.CultureInfo.CurrentCulture)),
        ];

        var texts = ResultTexts(detail).Append(new PrintText("Remarks", report.Remarks)).ToList();

        return Result<PrintableReport>.Success(new PrintableReport(
            Layout,
            Title,
            letterhead,
            patient,
            ResultLines(detail),
            texts,
            [new PrintSignatory("Medical Technologist", report.MedicalTechnologist), new PrintSignatory("Pathologist", report.Pathologist)],
            FooterNote));
    }
}
