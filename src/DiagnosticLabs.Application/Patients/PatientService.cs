using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Codes;
using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Domain.Patients;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Application.Patients;

public sealed record PatientListItem(
    long Id,
    string PatientCode,
    string PatientName,
    DateOnly? DateOfBirth,
    string? Age,
    string? Sex,
    string? ContactNumbers) : IHasId;

/// <summary><see cref="Age"/> is the typed age text, only meaningful while <see cref="DateOfBirth"/> is empty.</summary>
public sealed record PatientDetails(
    long Id,
    string PatientCode,
    string PatientName,
    DateOnly? DateOfBirth,
    string? Age,
    string? Sex,
    string? CivilStatus,
    string Address,
    string? ContactNumbers,
    byte[] RowVersion) : IHasId;

public sealed record PatientInput(
    long Id,
    string? PatientName,
    DateOnly? DateOfBirth,
    string? Age,
    string? Sex,
    string? CivilStatus,
    string? Address,
    string? ContactNumbers,
    byte[]? RowVersion) : ICrudInput;

public interface IPatientService : ICrudService<PatientListItem, PatientDetails, PatientInput>;

public sealed class PatientService(
    IAppDbContext db,
    ICurrentUser currentUser,
    ICodeGenerator codes,
    IClock clock)
    : CrudService<Patient, PatientListItem, PatientDetails, PatientInput>(db, currentUser, ModuleIds.Patients, "Patient"),
      IPatientService
{
    public const int NameMaxLength = 200;
    public const int AddressMaxLength = 500;

    private DateOnly Today => DateOnly.FromDateTime(clock.UtcNow.ToLocalTime());

    protected override DbSet<Patient> Set => Db.Patients;

    protected override IQueryable<Patient> Matches(IQueryable<Patient> query, string word) =>
        query.Where(p => p.PatientName.Contains(word) || p.PatientCode.Contains(word));

    protected override IOrderedQueryable<Patient> Order(IQueryable<Patient> query) =>
        query.OrderBy(p => p.PatientName).ThenBy(p => p.Id);

    // Soft-deleted rows are already hidden by the global query filter.
    protected override IQueryable<Patient> ApplyActiveFilter(IQueryable<Patient> query, bool includeInactive) => query;

    protected override void Remove(Patient entity) => Set.Remove(entity);

    // Removing a patient would hide every registration made for them.
    protected override async Task<Error?> CheckCanDeleteAsync(Patient entity, CancellationToken cancellationToken) =>
        await Db.PatientRegistrations.AnyAsync(r => r.PatientId == entity.Id, cancellationToken)
            ? new Error("Patient.HasRegistrations", "This patient has registrations, so they can not be deleted.")
            : null;

    protected override PatientListItem ToListItem(Patient p) =>
        new(p.Id, p.PatientCode, p.PatientName, p.DateOfBirth, AgeCalculator.Describe(p.DateOfBirth, Today) ?? p.Age, p.Sex, p.ContactNumbers);

    protected override PatientDetails ToDetails(Patient p) =>
        new(p.Id, p.PatientCode, p.PatientName, p.DateOfBirth, p.Age, p.Sex, p.CivilStatus, p.Address, p.ContactNumbers, p.RowVersion);

    protected override IReadOnlyList<string> Validate(PatientInput input) => Validate(input, Today);

    protected override async Task<Patient> CreateAsync(PatientInput input, CancellationToken cancellationToken) =>
        new() { PatientCode = await codes.NextPatientCodeAsync(cancellationToken) };

    protected override void Apply(Patient patient, PatientInput input)
    {
        patient.PatientName = input.PatientName!.Trim();
        patient.DateOfBirth = input.DateOfBirth;
        patient.Age = input.DateOfBirth is null ? Clean(input.Age) : null; // a known birth date wins over typed text
        patient.Sex = Clean(input.Sex);
        patient.CivilStatus = Clean(input.CivilStatus);
        patient.Address = input.Address?.Trim() ?? string.Empty;
        patient.ContactNumbers = Clean(input.ContactNumbers);
    }

    public static IReadOnlyList<string> Validate(PatientInput input, DateOnly today)
    {
        var errors = new List<string>();

        var name = input.PatientName?.Trim() ?? string.Empty;
        if (name.Length == 0)
            errors.Add("Name can not be empty.");
        else if (name.Length > NameMaxLength)
            errors.Add($"Name can not be longer than {NameMaxLength} characters.");

        if (input.DateOfBirth is { } birth && birth > today)
            errors.Add("Date of birth can not be in the future.");

        if ((input.Age?.Trim().Length ?? 0) > 50)
            errors.Add("Age can not be longer than 50 characters.");

        if ((input.Address?.Trim().Length ?? 0) > AddressMaxLength)
            errors.Add($"Address can not be longer than {AddressMaxLength} characters.");

        if ((input.ContactNumbers?.Trim().Length ?? 0) > 50)
            errors.Add("Contact numbers can not be longer than 50 characters.");

        if ((input.Sex?.Trim().Length ?? 0) > 20)
            errors.Add("Gender can not be longer than 20 characters.");

        if ((input.CivilStatus?.Trim().Length ?? 0) > 20)
            errors.Add("Civil status can not be longer than 20 characters.");

        return errors;
    }
}
