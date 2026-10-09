using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Billing;
using DiagnosticLabs.Application.Codes;
using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Application.Patients;
using DiagnosticLabs.Domain.Patients;
using DiagnosticLabs.Domain.Registrations;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Application.Registrations;

/// <summary>A service on a registration, at the price charged for it. <c>Id = 0</c> is a new line.</summary>
public sealed record RegistrationServiceLine(long Id, long ServiceId, decimal Price, string? ServiceName = null);

public sealed record RegistrationListItem(
    long Id, string RegistrationCode, string PatientName, string? CompanyName, DateOnly Date, decimal AmountDue) : IHasId;

/// <summary>Search text (registration code, patient name or patient code) plus optional company and date filters.</summary>
public sealed record RegistrationSearch(
    string? Text,
    long? CompanyId = null,
    DateOnly? Date = null,
    int Page = 1,
    int PageSize = Paging.DefaultPageSize,
    bool IncludeInactive = false) : CrudSearch(Text, Page, PageSize, IncludeInactive);

public sealed record RegistrationDetails(
    long Id,
    string RegistrationCode,
    DateOnly Date,
    long? CompanyId,
    string BatchName,
    long? PackageId,
    long PatientId,
    string PatientCode,
    string PatientName,
    DateOnly? DateOfBirth,
    string? Age,
    string? Sex,
    string? CivilStatus,
    string Address,
    string? ContactNumbers,
    byte[] PatientRowVersion,
    decimal AmountDue,
    IReadOnlyList<RegistrationServiceLine> Services,
    byte[] RowVersion) : IHasId;

/// <summary>
/// The registration form as a whole. <see cref="PatientId"/> 0 creates a new patient from the typed details;
/// otherwise the existing patient is used and their details are updated from the form.
/// </summary>
public sealed record RegistrationInput(
    long Id,
    DateOnly Date,
    long? CompanyId,
    string? BatchName,
    long? PackageId,
    long PatientId,
    string? PatientName,
    DateOnly? DateOfBirth,
    string? Age,
    string? Sex,
    string? CivilStatus,
    string? Address,
    string? ContactNumbers,
    byte[]? PatientRowVersion,
    decimal AmountDue,
    IReadOnlyList<RegistrationServiceLine> Services,
    byte[]? RowVersion) : ICrudInput;

/// <summary>An existing patient matching what is being typed in the name box.</summary>
public sealed record PatientSuggestion(long Id, string PatientCode, string PatientName, string? Age, string? Sex);

/// <summary>The codes a new registration form shows before saving; the real ones are assigned when it is saved.</summary>
public sealed record RegistrationPreview(string? RegistrationCode, string PatientCode);

public interface IRegistrationService : ICrudService<RegistrationListItem, RegistrationDetails, RegistrationInput>
{
    Task<Result<RegistrationPreview>> PreviewAsync(DateOnly date, CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<PatientSuggestion>>> SuggestPatientsAsync(string text, CancellationToken cancellationToken = default);

    Task<Result<PatientDetails>> GetPatientAsync(long patientId, CancellationToken cancellationToken = default);
}

public sealed class RegistrationService(IAppDbContext db, ICurrentUser currentUser, ICodeGenerator codes, IClock clock)
    : CrudService<PatientRegistration, RegistrationListItem, RegistrationDetails, RegistrationInput>(
        db, currentUser, ModuleIds.PatientRegistrations, "Registration"),
      IRegistrationService
{
    public const int MaxSuggestions = 8;
    public const int MinSuggestionLength = 2;

    private DateOnly Today => DateOnly.FromDateTime(clock.UtcNow.ToLocalTime());

    protected override DbSet<PatientRegistration> Set => Db.PatientRegistrations;

    protected override IQueryable<PatientRegistration> ForListing(IQueryable<PatientRegistration> q) =>
        q.Include(r => r.Patient).Include(r => r.Company);

    protected override IQueryable<PatientRegistration> ForEditing(IQueryable<PatientRegistration> q) =>
        q.Include(r => r.Patient).Include(r => r.Services).ThenInclude(s => s.Service);

    protected override IQueryable<PatientRegistration> Matches(IQueryable<PatientRegistration> q, string word) =>
        q.Where(r => r.RegistrationCode.Contains(word) || r.Patient.PatientName.Contains(word) || r.Patient.PatientCode.Contains(word));

    protected override IOrderedQueryable<PatientRegistration> Order(IQueryable<PatientRegistration> q) =>
        q.OrderByDescending(r => r.InputDate).ThenByDescending(r => r.Id);

    // Soft-deleted rows are already hidden by the global query filter.
    protected override IQueryable<PatientRegistration> ApplyActiveFilter(IQueryable<PatientRegistration> q, bool includeInactive) => q;

    protected override void Remove(PatientRegistration entity)
    {
        // The service lines go with the registration (they are loaded by CheckCanDeleteAsync).
        foreach (var line in entity.Services.ToList())
            Db.PatientRegistrationServices.Remove(line);

        Set.Remove(entity);
    }

    protected override IQueryable<PatientRegistration> Filter(IQueryable<PatientRegistration> q, CrudSearch search)
    {
        if (search is not RegistrationSearch filters)
            return q;

        if (filters.CompanyId is { } companyId)
            q = q.Where(r => r.CompanyId == companyId);

        if (filters.Date is { } date)
        {
            var (from, to) = DayBoundsUtc(date);
            q = q.Where(r => r.InputDate >= from && r.InputDate < to);
        }

        return q;
    }

    protected override RegistrationListItem ToListItem(PatientRegistration r) =>
        new(r.Id, r.RegistrationCode, r.Patient.PatientName, r.Company?.CompanyName, LocalDate(r.InputDate), r.AmountDue);

    protected override RegistrationDetails ToDetails(PatientRegistration r) =>
        new(r.Id, r.RegistrationCode, LocalDate(r.InputDate), r.CompanyId, r.BatchName, r.PackageId,
            r.PatientId, r.Patient.PatientCode, r.Patient.PatientName, r.Patient.DateOfBirth, r.Patient.Age, r.Patient.Sex,
            r.Patient.CivilStatus, r.Patient.Address, r.Patient.ContactNumbers, r.Patient.RowVersion, r.AmountDue,
            [.. r.Services.Where(s => !s.IsDeleted).OrderBy(s => s.Id)
                .Select(s => new RegistrationServiceLine(s.Id, s.ServiceId, s.Price, s.Service?.ServiceName))],
            r.RowVersion);

    // ---------------------------------------------------------------- validation

    protected override IReadOnlyList<string> Validate(RegistrationInput input)
    {
        var errors = new List<string>();

        // The patient part follows the same rules as the Patients screen.
        errors.AddRange(PatientService.Validate(
            new PatientInput(input.PatientId, input.PatientName, input.DateOfBirth, input.Age, input.Sex, input.CivilStatus, input.Address, input.ContactNumbers, null),
            Today));

        if ((input.BatchName?.Trim().Length ?? 0) > 200)
            errors.Add("Batch can not be longer than 200 characters.");

        if (input.Services.Count == 0)
            errors.Add("Add at least one service.");
        if (input.Services.Any(s => s.Price < 0))
            errors.Add("Service prices must not be negative.");
        if (input.Services.GroupBy(s => s.ServiceId).Any(g => g.Count() > 1))
            errors.Add("A service can only be listed once.");
        if (input.AmountDue < 0)
            errors.Add("Price must not be negative.");

        return errors;
    }

    protected override async Task<IReadOnlyList<string>> ValidateAsync(RegistrationInput input, CancellationToken cancellationToken)
    {
        var errors = new List<string>();

        if (input.CompanyId is { } companyId && !await Db.Companies.AnyAsync(c => c.Id == companyId, cancellationToken))
            errors.Add("The selected company no longer exists.");

        if (input.PackageId is { } packageId && !await Db.Packages.AnyAsync(p => p.Id == packageId, cancellationToken))
            errors.Add("The selected package no longer exists.");

        var serviceIds = input.Services.Select(s => s.ServiceId).Distinct().ToList();
        if (await Db.Services.CountAsync(s => serviceIds.Contains(s.Id), cancellationToken) != serviceIds.Count)
            errors.Add("One or more services no longer exist.");

        if (input.PatientId != 0 && !await Db.Patients.AnyAsync(p => p.Id == input.PatientId, cancellationToken))
            errors.Add("The selected patient no longer exists.");

        if (input.Id == 0)
        {
            if (await codes.PeekRegistrationCodeAsync(Today, cancellationToken) is null)
                errors.Add("The company code has not been set up yet. An administrator can set it under Settings > Company Setup.");
        }
        else
        {
            var patientId = await Db.PatientRegistrations.Where(r => r.Id == input.Id).Select(r => (long?)r.PatientId).FirstOrDefaultAsync(cancellationToken);
            if (patientId is { } existing && existing != input.PatientId)
                errors.Add("A registration can not be moved to a different patient.");
        }

        return errors;
    }

    // ---------------------------------------------------------------- deleting

    protected override async Task<Error?> CheckCanDeleteAsync(PatientRegistration entity, CancellationToken cancellationToken)
    {
        // Both sets already ignore soft-deleted rows.
        if (await Db.Payments.AnyAsync(p => p.PatientRegistrationId == entity.Id, cancellationToken)
            || await Db.LabReports.AnyAsync(l => l.PatientRegistrationId == entity.Id, cancellationToken))
        {
            return new Error("Registration.InUse", "This registration already has payments or lab results, so it can not be deleted.");
        }

        return null;
    }

    // ---------------------------------------------------------------- saving

    protected override async Task<PatientRegistration> CreateAsync(RegistrationInput input, CancellationToken cancellationToken)
    {
        // Validation has passed, so using up the numbers now is safe.
        var registration = new PatientRegistration
        {
            RegistrationCode = await codes.NextRegistrationCodeAsync(Today, cancellationToken),
            DiscountAmount = 0,
        };

        registration.Patient = input.PatientId == 0
            ? new Patient { PatientCode = await codes.NextPatientCodeAsync(cancellationToken) }
            : await Db.Patients.FirstAsync(p => p.Id == input.PatientId, cancellationToken);

        return registration;
    }

    protected override void Apply(PatientRegistration registration, RegistrationInput input)
    {
        // Keep the time of day of a registration whose date was not changed; a new or moved one gets "now" on that date.
        if (registration.InputDate == default || LocalDate(registration.InputDate) != input.Date)
            registration.InputDate = ToUtc(input.Date, clock.UtcNow.ToLocalTime().TimeOfDay);

        registration.CompanyId = input.CompanyId;
        registration.BatchName = input.BatchName?.Trim() ?? string.Empty;
        registration.PackageId = input.PackageId;
        registration.AmountDue = input.AmountDue;

        // A percentage discount follows the price, and no discount may exceed it.
        registration.DiscountTotal = BillingMath.DiscountTotal(registration.AmountDue, registration.DiscountAmount, registration.DiscountPercentage);

        var patient = registration.Patient;
        if (input.PatientRowVersion is { Length: > 0 } && patient.Id != 0 && patient.Id == input.PatientId)
            Db.SetOriginalRowVersion(patient, input.PatientRowVersion);

        patient.PatientName = input.PatientName!.Trim();
        patient.DateOfBirth = input.DateOfBirth;
        patient.Age = input.DateOfBirth is null ? Clean(input.Age) : null;
        patient.Sex = Clean(input.Sex);
        patient.CivilStatus = Clean(input.CivilStatus);
        patient.Address = input.Address?.Trim() ?? string.Empty;
        patient.ContactNumbers = Clean(input.ContactNumbers);

        SyncChildren(
            registration.Services,
            input.Services,
            line => line.Id,
            child => child.Id,
            (child, line) => child.ServiceId == line.ServiceId,
            (child, line) =>
            {
                child.ServiceId = line.ServiceId;
                child.Price = line.Price;
            },
            child => Db.PatientRegistrationServices.Remove(child));
    }

    protected override async Task AfterSaveAsync(PatientRegistration registration, RegistrationInput input, CancellationToken cancellationToken)
    {
        // Newly added lines have no Service loaded yet; load them so the saved registration can show service names.
        var missing = registration.Services.Where(s => !s.IsDeleted && s.Service is null).Select(s => s.ServiceId).Distinct().ToList();
        if (missing.Count > 0)
            await Db.Services.Where(s => missing.Contains(s.Id)).ToListAsync(cancellationToken);
    }

    // ---------------------------------------------------------------- helpers for the form

    public async Task<Result<RegistrationPreview>> PreviewAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        if (!CurrentUser.Can(ModuleIds.PatientRegistrations, ModuleAction.View))
            return Result<RegistrationPreview>.Failure(Errors.Forbidden);

        return Result<RegistrationPreview>.Success(new RegistrationPreview(
            await codes.PeekRegistrationCodeAsync(date, cancellationToken),
            await codes.PeekPatientCodeAsync(cancellationToken)));
    }

    public async Task<Result<IReadOnlyList<PatientSuggestion>>> SuggestPatientsAsync(string text, CancellationToken cancellationToken = default)
    {
        if (!CurrentUser.Can(ModuleIds.PatientRegistrations, ModuleAction.View))
            return Result<IReadOnlyList<PatientSuggestion>>.Failure(Errors.Forbidden);

        var words = (text ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length == 0 || string.Concat(words).Length < MinSuggestionLength)
            return Result<IReadOnlyList<PatientSuggestion>>.Success([]);

        var query = Db.Patients.AsNoTracking();
        foreach (var word in words)
        {
            var term = word;
            query = query.Where(p => p.PatientName.Contains(term) || p.PatientCode.Contains(term));
        }

        var rows = await query.OrderBy(p => p.PatientName).ThenBy(p => p.Id).Take(MaxSuggestions)
            .Select(p => new { p.Id, p.PatientCode, p.PatientName, p.DateOfBirth, p.Age, p.Sex })
            .ToListAsync(cancellationToken);

        IReadOnlyList<PatientSuggestion> suggestions =
        [
            .. rows.Select(r => new PatientSuggestion(r.Id, r.PatientCode, r.PatientName, AgeCalculator.Describe(r.DateOfBirth, Today) ?? r.Age, r.Sex)),
        ];

        return Result<IReadOnlyList<PatientSuggestion>>.Success(suggestions);
    }

    public async Task<Result<PatientDetails>> GetPatientAsync(long patientId, CancellationToken cancellationToken = default)
    {
        if (!CurrentUser.Can(ModuleIds.PatientRegistrations, ModuleAction.View))
            return Result<PatientDetails>.Failure(Errors.Forbidden);

        var p = await Db.Patients.AsNoTracking().FirstOrDefaultAsync(x => x.Id == patientId, cancellationToken);
        return p is null
            ? Result<PatientDetails>.Failure(Errors.NotFound("Patient"))
            : Result<PatientDetails>.Success(new PatientDetails(
                p.Id, p.PatientCode, p.PatientName, p.DateOfBirth, p.Age, p.Sex, p.CivilStatus, p.Address, p.ContactNumbers, p.RowVersion));
    }

    // Dates are chosen as local calendar days but stored as UTC instants.
    private static DateOnly LocalDate(DateTime utc) => DateOnly.FromDateTime(DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime());

    private static DateTime ToUtc(DateOnly date, TimeSpan timeOfDay) =>
        new DateTime(date.Year, date.Month, date.Day, 0, 0, 0, DateTimeKind.Local).Add(timeOfDay).ToUniversalTime();

    private static (DateTime From, DateTime To) DayBoundsUtc(DateOnly date) =>
        (ToUtc(date, TimeSpan.Zero), ToUtc(date.AddDays(1), TimeSpan.Zero));
}
