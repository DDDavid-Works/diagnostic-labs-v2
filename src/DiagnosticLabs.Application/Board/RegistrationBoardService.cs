using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Application.LabResults;
using DiagnosticLabs.Domain.Billing;
using DiagnosticLabs.Domain.Catalog;
using DiagnosticLabs.Domain.Lab;
using DiagnosticLabs.Domain.Lab.Reports;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Application.Board;

public enum BoardStatus
{
    All,

    /// <summary>Not charged to the company and not fully paid.</summary>
    Unpaid,

    /// <summary>At least one of the result forms the registration's services call for has no result yet.</summary>
    ResultsPending,
}

public enum PaymentState
{
    Unpaid,
    Partial,
    Paid,
    Charged,
}

/// <summary>The registrations to show: a day (or a range of days), who, which company, and a status. Newest first, paged.</summary>
public sealed record BoardSearch(
    string? Text,
    long? CompanyId = null,
    DateOnly? From = null,
    DateOnly? To = null,
    BoardStatus Status = BoardStatus.All,
    int Page = 1,
    int PageSize = Paging.DefaultPageSize);

public sealed record BoardPayment(PaymentState State, decimal Paid, decimal Due);

/// <summary>A form the user can choose to enter a service's result in (<see cref="Section"/> set for a section of the Physical Examination sheet).</summary>
public sealed record BoardFormChoice(int ModuleId, string FormName, FormSection? Section, long? SheetId);

/// <summary>
/// One service of a registration. <see cref="ModuleId"/> is the result form that service's chip opens (null when it has none, or while the
/// user must still choose among <see cref="Choices"/>); <see cref="CanOpen"/> is false while that form is not built yet. <see cref="ResultCount"/>
/// is how many results exist for it and <see cref="LatestResultId"/> the latest. <see cref="Section"/> is set when the result goes in a section of
/// the Physical Examination sheet, and <see cref="OpenResultId"/> is the result to open (the latest result, or the registration's sheet that does
/// not have this section yet). <see cref="RegistrationServiceId"/> is the registration line, where a choice is kept.
/// </summary>
public sealed record BoardService(
    long ServiceId,
    string Name,
    int? ModuleId,
    bool CanOpen,
    int ResultCount,
    long? LatestResultId,
    long RegistrationServiceId = 0,
    FormSection? Section = null,
    long? OpenResultId = null,
    IReadOnlyList<BoardFormChoice>? Choices = null)
{
    public bool NeedsChoice => Choices is { Count: > 0 };
}

public sealed record BoardItem(
    long RegistrationId,
    string RegistrationCode,
    string PatientName,
    DateOnly Date,
    string? CompanyName,
    string BatchName,
    BoardPayment Payment,
    IReadOnlyList<BoardService> Services);

/// <summary>Which result form belongs to which report type (the forms that are built).</summary>
public static class LabResultModules
{
    private static readonly Dictionary<int, LabReportType> Types = new()
    {
        [ModuleIds.StoolFecalysis] = LabReportType.StoolFecalysis,
        [ModuleIds.Urinalysis] = LabReportType.Urinalysis,
        [ModuleIds.Hematology] = LabReportType.Hematology,
        [ModuleIds.Immunology] = LabReportType.Immunology,
        [ModuleIds.Serology] = LabReportType.Serology,
        [ModuleIds.PregnancyTest] = LabReportType.PregnancyTest,
        [ModuleIds.ClinicalChemistry] = LabReportType.ClinicalChemistry,
        [ModuleIds.ClinicalChemistry1] = LabReportType.ClinicalChemistry1,
        [ModuleIds.ClinicalChemistry2] = LabReportType.ClinicalChemistry2,
        [ModuleIds.AnnualPhysicalExam] = LabReportType.AnnualPhysicalExam,
        [ModuleIds.PhysicalExamination] = LabReportType.PhysicalExamination,
        [ModuleIds.ClinicalMicroscopy] = LabReportType.ClinicalMicroscopy,
        [ModuleIds.MedicalExamReport] = LabReportType.MedicalExamReport,
    };

    public static LabReportType? ReportTypeOf(int moduleId) => Types.TryGetValue(moduleId, out var type) ? type : null;

    /// <summary>A form made in the Custom Lab Form designer (its module id is 1000 or more); its results are told apart by the form, not only by their type.</summary>
    public static bool IsCustomForm(int moduleId) => moduleId >= ModuleIds.FirstCustomForm;
}

/// <summary>The home screen's list: registrations with what has been paid and which results have been made.</summary>
public interface IRegistrationBoardService
{
    Task<Result<PagedResult<BoardItem>>> SearchAsync(BoardSearch search, CancellationToken cancellationToken = default);

    /// <summary>Keeps where a registration line's result is entered, for a service that can go in more than one form.</summary>
    Task<Result> ChooseFormAsync(long registrationServiceId, int moduleId, CancellationToken cancellationToken = default);
}

public sealed class RegistrationBoardService(IAppDbContext db, ICurrentUser currentUser) : IRegistrationBoardService
{
    /// <summary>The registration the old app kept for its "Default Patient"; it is not a real visit.</summary>
    public const string DefaultRegistrationCode = "000-000-000";

    public const int MinSearchLength = 2;

    public async Task<Result<PagedResult<BoardItem>>> SearchAsync(BoardSearch search, CancellationToken cancellationToken = default)
    {
        if (!currentUser.CanAccess(ModuleIds.PatientRegistrations) && !currentUser.CanAccess(ModuleIds.Payments))
            return Result<PagedResult<BoardItem>>.Failure(Errors.Forbidden);

        var (page, pageSize) = Paging.Normalize(search.Page, search.PageSize);

        // Which services call for which result forms (a module linked to its service, and the sections of the Physical Examination sheet).
        var formsOfService = await ResultFormLinks.LoadAsync(db, activeOnly: false, cancellationToken);

        var query = db.PatientRegistrations.AsNoTracking().Where(r => r.RegistrationCode != DefaultRegistrationCode);

        foreach (var word in (search.Text ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var term = word;
            query = query.Where(r => r.RegistrationCode.Contains(term) || r.Patient.PatientName.Contains(term) || r.Patient.PatientCode.Contains(term));
        }

        if (search.CompanyId is { } companyId)
            query = query.Where(r => r.CompanyId == companyId);

        if (search.From is { } from)
            query = query.Where(r => r.InputDate >= LocalTime.DayBoundsUtc(from).From);
        if (search.To is { } to)
            query = query.Where(r => r.InputDate < LocalTime.DayBoundsUtc(to).To);

        if (search.Status == BoardStatus.Unpaid)
        {
            query = query.Where(r => !r.Payments.Any(p => p.Type == PaymentType.Charge)
                && (r.Payments.Where(p => p.Type == PaymentType.Payment).Sum(p => (decimal?)p.PaymentAmount) ?? 0m) < r.AmountDue - r.DiscountTotal);
        }
        else if (search.Status == BoardStatus.ResultsPending)
        {
            // A service is pending while none of its forms has its result (Urinalysis counts whether it went on its own form or on the sheet).
            IQueryable<long>? pending = null;
            foreach (var (serviceId, forms) in formsOfService)
            {
                var done = forms.Select(f => ResultFormLinks.Done(db, f)).OfType<IQueryable<long?>>().ToList();
                if (done.Count == 0)
                    continue;

                var sid = serviceId;
                var ids = db.PatientRegistrationServices.Where(s => s.ServiceId == sid).Select(s => s.PatientRegistrationId);
                foreach (var made in done)
                {
                    var registrationsWithIt = made;
                    ids = ids.Where(id => !registrationsWithIt.Contains(id));
                }

                pending = pending is null ? ids : pending.Union(ids);
            }

            if (pending is null)
                return Result<PagedResult<BoardItem>>.Success(PagedResult<BoardItem>.Empty(page, pageSize));

            var pendingIds = pending;
            query = query.Where(r => pendingIds.Contains(r.Id));
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderByDescending(r => r.InputDate).ThenByDescending(r => r.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(r => new
            {
                r.Id,
                r.RegistrationCode,
                r.Patient.PatientName,
                r.InputDate,
                CompanyName = r.Company != null ? r.Company.CompanyName : null,
                r.BatchName,
                r.AmountDue,
                r.DiscountTotal,
                Paid = r.Payments.Where(p => p.Type == PaymentType.Payment).Sum(p => (decimal?)p.PaymentAmount) ?? 0m,
                Charged = r.Payments.Any(p => p.Type == PaymentType.Charge),
                Services = r.Services.OrderBy(s => s.Id).Select(s => new { s.Id, s.ServiceId, s.Service.ServiceName, s.ResultModuleId }).ToList(),
            })
            .ToListAsync(cancellationToken);

        // Which results exist for these registrations (a handful of rows, grouped here).
        var registrationIds = rows.Select(r => (long?)r.Id).ToList();
        var results = await db.LabReports.AsNoTracking()
            .Where(l => registrationIds.Contains(l.PatientRegistrationId))
            .Select(l => new
            {
                RegistrationId = l.PatientRegistrationId!.Value,
                l.ReportType,
                l.Id,
                // for a result of a form made in the Custom Lab Form designer: which form (its module)
                CustomModuleId = db.CustomFormReports.Where(c => c.LabReportId == l.Id).Select(c => (int?)c.CustomFormVersion.CustomForm.ModuleId).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        // The Physical Examination sheets of these registrations, to tell which of their sections are entered.
        var sheets = formsOfService.Values.Any(forms => forms.Any(f => f.Section is not null))
            ? await db.Set<PhysicalExaminationReport>().AsNoTracking()
                .Where(d => registrationIds.Contains(d.LabReport.PatientRegistrationId))
                .Select(d => new { RegistrationId = d.LabReport.PatientRegistrationId!.Value, Sheet = d })
                .ToListAsync(cancellationToken)
            : [];
        var formNames = await db.Modules.AsNoTracking().ToDictionaryAsync(m => m.Id, m => m.ModuleName, cancellationToken);

        var items = rows.Select(r =>
        {
            var due = Math.Max(r.AmountDue - r.DiscountTotal, 0m);
            var state = r.Charged ? PaymentState.Charged
                : r.Paid >= due ? PaymentState.Paid
                : r.Paid > 0m ? PaymentState.Partial
                : PaymentState.Unpaid;

            var ownSheets = sheets.Where(x => x.RegistrationId == r.Id).OrderBy(x => x.Sheet.LabReportId).ToList();
            long? sheetId = ownSheets.Count > 0 ? ownSheets[^1].Sheet.LabReportId : null;

            // The results of one form for this registration, oldest first.
            List<long> Made(ServiceForm form)
            {
                if (form.Section is { } section)
                    return [.. ownSheets.Where(x => PhysicalExaminationFields.IsFilled(section, x.Sheet)).Select(x => x.Sheet.LabReportId)];

                if (ResultFormLinks.TypeOf(form.ModuleId) is not { } type)
                    return [];

                var custom = LabResultModules.IsCustomForm(form.ModuleId);
                return [.. results.Where(x => x.RegistrationId == r.Id && x.ReportType == type && (!custom || x.CustomModuleId == form.ModuleId)).OrderBy(x => x.Id).Select(x => x.Id)];
            }

            var services = r.Services.Select(s =>
            {
                var forms = formsOfService.TryGetValue(s.ServiceId, out var f) ? f : [];
                if (forms.Count == 0)
                    return new BoardService(s.ServiceId, s.ServiceName, null, false, 0, null, s.Id);

                var made = forms.Select(form => (Form: form, Ids: Made(form))).ToList();

                // Made somewhere: the chip opens the latest result, wherever it was entered.
                var withResult = made.Where(m => m.Ids.Count > 0).OrderBy(m => m.Ids[^1]).ToList();
                if (withResult.Count > 0)
                {
                    var (form, ids) = withResult[^1];
                    return new BoardService(s.ServiceId, s.ServiceName, form.ModuleId, true, ids.Count, ids[^1], s.Id, form.Section, ids[^1]);
                }

                // Not made yet: the only form, or the one chosen for this registration; otherwise the user chooses.
                var chosen = forms.Count == 1 ? forms[0] : forms.FirstOrDefault(x => x.ModuleId == s.ResultModuleId);
                if (chosen is not null)
                {
                    var built = chosen.Section is not null || ResultFormLinks.TypeOf(chosen.ModuleId) is not null;
                    return new BoardService(s.ServiceId, s.ServiceName, chosen.ModuleId, built, 0, null, s.Id, chosen.Section, chosen.Section is null ? null : sheetId);
                }

                IReadOnlyList<BoardFormChoice> choices =
                [
                    .. forms.Where(x => x.Section is not null || ResultFormLinks.TypeOf(x.ModuleId) is not null)
                        .Select(x => new BoardFormChoice(
                            x.ModuleId,
                            x.Section is null ? formNames.GetValueOrDefault(x.ModuleId, s.ServiceName) : formNames.GetValueOrDefault(x.ModuleId, "Physical Examination") + " sheet",
                            x.Section,
                            x.Section is null ? null : sheetId)),
                ];
                return new BoardService(s.ServiceId, s.ServiceName, null, choices.Count > 0, 0, null, s.Id, Choices: choices);
            }).ToList();

            return new BoardItem(
                r.Id, r.RegistrationCode, r.PatientName, LocalTime.ToLocalDate(r.InputDate), r.CompanyName, r.BatchName,
                new BoardPayment(state, r.Paid, due), services);
        }).ToList();

        return Result<PagedResult<BoardItem>>.Success(new PagedResult<BoardItem>(items, total, page, pageSize));
    }

    public async Task<Result> ChooseFormAsync(long registrationServiceId, int moduleId, CancellationToken cancellationToken = default)
    {
        if (!currentUser.Can(moduleId, ModuleAction.Create))
            return Result.Failure(Errors.Forbidden);

        var line = await db.PatientRegistrationServices.FirstOrDefaultAsync(s => s.Id == registrationServiceId, cancellationToken);
        if (line is null)
            return Result.Failure(Errors.NotFound("Registration service"));

        var forms = await ResultFormLinks.LoadAsync(db, activeOnly: false, cancellationToken);
        if (!forms.TryGetValue(line.ServiceId, out var list) || list.All(f => f.ModuleId != moduleId))
            return Result.Failure(Errors.Invalid(["That form is not one this service's result can be entered in."]));

        line.ResultModuleId = moduleId;
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}