using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Domain.Settings;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Application.Codes;

public interface ICodeGenerator
{
    /// <summary>Next patient code, e.g. <c>2026-00000042</c>. Numbers restart each calendar year.</summary>
    Task<string> NextPatientCodeAsync(CancellationToken cancellationToken = default);

    /// <summary>What <see cref="NextPatientCodeAsync"/> would hand out right now, without using it up.</summary>
    Task<string> PeekPatientCodeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Next registration code for <paramref name="day"/>, e.g. <c>BADC-09OC26-00001</c>: the company code, the date
    /// (two-letter month) and a counter that restarts every day. Fails if no company code has been set up.
    /// </summary>
    Task<string> NextRegistrationCodeAsync(DateOnly day, CancellationToken cancellationToken = default);

    /// <summary>The next registration code without using it up, or <c>null</c> when no company code has been set up.</summary>
    Task<string?> PeekRegistrationCodeAsync(DateOnly day, CancellationToken cancellationToken = default);
}

/// <summary>
/// Hands out sequence numbers from the CodeSequences table. Two users asking at the same moment
/// cannot receive the same number: the row is guarded by a row version, and the loser retries.
/// </summary>
public sealed class CodeGenerator(IAppDbContext db, IClock clock) : ICodeGenerator
{
    private const int MaxAttempts = 5;

    private static readonly string[] MonthCodes = ["JA", "FE", "MR", "AP", "MY", "JN", "JL", "AU", "SE", "OC", "NO", "DE"];

    public async Task<string> NextPatientCodeAsync(CancellationToken cancellationToken = default)
    {
        var (key, yearPrefix, seed) = PatientSequence();
        return $"{yearPrefix}{await NextNumberAsync(key, seed, cancellationToken):D8}";
    }

    public async Task<string> PeekPatientCodeAsync(CancellationToken cancellationToken = default)
    {
        var (key, yearPrefix, seed) = PatientSequence();
        return $"{yearPrefix}{await PeekNumberAsync(key, seed, cancellationToken):D8}";
    }

    public async Task<string> NextRegistrationCodeAsync(DateOnly day, CancellationToken cancellationToken = default)
    {
        var company = await CompanyCodeAsync(cancellationToken)
                      ?? throw new InvalidOperationException("The company code has not been set up (Settings > Company Setup).");
        var prefix = DayPrefix(day);
        var number = await NextNumberAsync(prefix, ct => SeedRegistrationAsync(prefix, ct), cancellationToken);
        return $"{company}-{prefix}-{number:D5}";
    }

    public async Task<string?> PeekRegistrationCodeAsync(DateOnly day, CancellationToken cancellationToken = default)
    {
        var company = await CompanyCodeAsync(cancellationToken);
        if (company is null)
            return null;

        var prefix = DayPrefix(day);
        return $"{company}-{prefix}-{await PeekNumberAsync(prefix, ct => SeedRegistrationAsync(prefix, ct), cancellationToken):D5}";
    }

    /// <summary><c>09OC26</c> for 9 October 2026; this is also the key of that day's counter (it matches the legacy counters).</summary>
    public static string DayPrefix(DateOnly day) => $"{day.Day:D2}{MonthCodes[day.Month - 1]}{day.Year % 100:D2}";

    private (string Key, string YearPrefix, Func<CancellationToken, Task<long>> Seed) PatientSequence()
    {
        var year = clock.UtcNow.ToLocalTime().Year;
        var yearPrefix = $"{year}-";
        return ($"PATIENT-{year}", yearPrefix, async ct =>
        {
            // Seed from existing codes (including deleted ones, so a code is never reused).
            var codes = await db.Patients.IgnoreQueryFilters()
                .Where(p => p.PatientCode.StartsWith(yearPrefix))
                .Select(p => p.PatientCode)
                .ToListAsync(ct);

            return codes
                .Select(c => long.TryParse(c.AsSpan(yearPrefix.Length), out var n) ? n : 0)
                .DefaultIfEmpty(0)
                .Max();
        });
    }

    private async Task<string?> CompanyCodeAsync(CancellationToken cancellationToken) =>
        await db.CompanySetups.AsNoTracking()
            .OrderByDescending(c => c.UpdatedAtUtc).ThenByDescending(c => c.Id)
            .Select(c => c.Code)
            .FirstOrDefaultAsync(cancellationToken) is { Length: > 0 } code
            ? code
            : null;

    private async Task<long> SeedRegistrationAsync(string prefix, CancellationToken cancellationToken)
    {
        var marker = $"-{prefix}-";
        var codes = await db.PatientRegistrations.IgnoreQueryFilters()
            .Where(r => r.RegistrationCode.Contains(marker))
            .Select(r => r.RegistrationCode)
            .ToListAsync(cancellationToken);

        return codes
            .Select(c => long.TryParse(c.AsSpan(c.LastIndexOf('-') + 1), out var n) ? n : 0)
            .DefaultIfEmpty(0)
            .Max();
    }

    private async Task<long> PeekNumberAsync(string key, Func<CancellationToken, Task<long>> seed, CancellationToken cancellationToken)
    {
        var last = await db.CodeSequences.AsNoTracking()
            .Where(s => s.Prefix == key)
            .Select(s => (long?)s.LastNumber)
            .FirstOrDefaultAsync(cancellationToken);

        return (last ?? await seed(cancellationToken)) + 1;
    }

    private async Task<long> NextNumberAsync(string key, Func<CancellationToken, Task<long>> seed, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            var sequence = await db.CodeSequences.FirstOrDefaultAsync(s => s.Prefix == key, cancellationToken);
            if (sequence is null)
            {
                sequence = new CodeSequence { Prefix = key, LastNumber = await seed(cancellationToken) };
                db.CodeSequences.Add(sequence);
            }

            sequence.LastNumber++;

            try
            {
                await db.SaveChangesAsync(cancellationToken);
                return sequence.LastNumber;
            }
            catch (DbUpdateException) when (attempt < MaxAttempts)
            {
                // Someone else created or advanced the row first: forget our copy and read it again.
                db.ChangeTracker.Clear();
            }
        }
    }
}
