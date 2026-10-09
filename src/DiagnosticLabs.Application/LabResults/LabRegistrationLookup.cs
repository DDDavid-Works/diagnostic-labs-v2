using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Domain.Patients;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Application.LabResults;

/// <summary>A registration as the top of a result form shows it, with the patient details that fill the form in.</summary>
public sealed record ResultRegistration(
    long RegistrationId,
    string RegistrationCode,
    DateOnly Date,
    long PatientId,
    string PatientCode,
    string PatientName,
    string? Age,
    string? Sex,
    string? CompanyName,
    string BatchName,
    IReadOnlyList<string> Services);

/// <summary>A registration offered while typing in the registration box.</summary>
public sealed record ResultRegistrationMatch(long Id, string RegistrationCode, string PatientName, DateOnly Date);

/// <summary>
/// Finds the registration a lab result is made for. Every result screen uses it; <c>moduleId</c> is the result screen's
/// own module, so the same permission that opens the screen allows the lookup.
/// </summary>
public interface ILabRegistrationLookup
{
    Task<Result<ResultRegistration>> FindAsync(int moduleId, string code, CancellationToken cancellationToken = default);

    Task<Result<ResultRegistration>> GetAsync(int moduleId, long registrationId, CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<ResultRegistrationMatch>>> SuggestAsync(int moduleId, string text, CancellationToken cancellationToken = default);
}

public sealed class LabRegistrationLookup(IAppDbContext db, ICurrentUser currentUser, IClock clock) : ILabRegistrationLookup
{
    public const int MaxSuggestions = 8;
    public const int MinSuggestionLength = 2;

    private DateOnly Today => LocalTime.ToLocalDate(clock.UtcNow);

    public async Task<Result<ResultRegistration>> FindAsync(int moduleId, string code, CancellationToken cancellationToken = default)
    {
        if (!currentUser.Can(moduleId, ModuleAction.View))
            return Result<ResultRegistration>.Failure(Errors.Forbidden);

        var text = (code ?? string.Empty).Trim();
        var id = text.Length == 0
            ? null
            : await db.PatientRegistrations.AsNoTracking().Where(r => r.RegistrationCode == text).Select(r => (long?)r.Id).FirstOrDefaultAsync(cancellationToken);

        return id is null
            ? Result<ResultRegistration>.Failure(new Error("Registration.NotFound", "No registration has that code."))
            : await GetAsync(moduleId, id.Value, cancellationToken);
    }

    public async Task<Result<ResultRegistration>> GetAsync(int moduleId, long registrationId, CancellationToken cancellationToken = default)
    {
        if (!currentUser.Can(moduleId, ModuleAction.View))
            return Result<ResultRegistration>.Failure(Errors.Forbidden);

        var r = await db.PatientRegistrations.AsNoTracking()
            .Include(x => x.Patient).Include(x => x.Company).Include(x => x.Services).ThenInclude(s => s.Service)
            .FirstOrDefaultAsync(x => x.Id == registrationId, cancellationToken);
        if (r is null)
            return Result<ResultRegistration>.Failure(new Error("Registration.NotFound", "The registration no longer exists."));

        return Result<ResultRegistration>.Success(new ResultRegistration(
            r.Id,
            r.RegistrationCode,
            LocalTime.ToLocalDate(r.InputDate),
            r.PatientId,
            r.Patient.PatientCode,
            r.Patient.PatientName,
            AgeCalculator.Describe(r.Patient.DateOfBirth, Today) ?? r.Patient.Age,
            r.Patient.Sex,
            r.Company?.CompanyName,
            r.BatchName,
            [.. r.Services.Where(s => !s.IsDeleted).OrderBy(s => s.Id).Select(s => s.Service.ServiceName)]));
    }

    public async Task<Result<IReadOnlyList<ResultRegistrationMatch>>> SuggestAsync(int moduleId, string text, CancellationToken cancellationToken = default)
    {
        if (!currentUser.Can(moduleId, ModuleAction.View))
            return Result<IReadOnlyList<ResultRegistrationMatch>>.Failure(Errors.Forbidden);

        var words = (text ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length == 0 || string.Concat(words).Length < MinSuggestionLength)
            return Result<IReadOnlyList<ResultRegistrationMatch>>.Success([]);

        var query = db.PatientRegistrations.AsNoTracking();
        foreach (var word in words)
        {
            var term = word;
            query = query.Where(r => r.RegistrationCode.Contains(term) || r.Patient.PatientName.Contains(term) || r.Patient.PatientCode.Contains(term));
        }

        var rows = await query.OrderByDescending(r => r.InputDate).ThenByDescending(r => r.Id).Take(MaxSuggestions)
            .Select(r => new { r.Id, r.RegistrationCode, r.Patient.PatientName, r.InputDate })
            .ToListAsync(cancellationToken);

        IReadOnlyList<ResultRegistrationMatch> matches =
            [.. rows.Select(r => new ResultRegistrationMatch(r.Id, r.RegistrationCode, r.PatientName, LocalTime.ToLocalDate(r.InputDate)))];
        return Result<IReadOnlyList<ResultRegistrationMatch>>.Success(matches);
    }
}
