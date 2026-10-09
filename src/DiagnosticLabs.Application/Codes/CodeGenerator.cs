using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Domain.Settings;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Application.Codes;

public interface ICodeGenerator
{
    /// <summary>Next patient code, e.g. <c>2026-00000042</c>. Numbers restart each calendar year.</summary>
    Task<string> NextPatientCodeAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Hands out sequence numbers from the CodeSequences table. Two users asking at the same moment
/// cannot receive the same number: the row is guarded by a row version, and the loser retries.
/// </summary>
public sealed class CodeGenerator(IAppDbContext db, IClock clock) : ICodeGenerator
{
    private const int MaxAttempts = 5;

    public async Task<string> NextPatientCodeAsync(CancellationToken cancellationToken = default)
    {
        var year = clock.UtcNow.ToLocalTime().Year;
        var yearPrefix = $"{year}-";

        var number = await NextNumberAsync(
            $"PATIENT-{year}",
            async ct =>
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
            },
            cancellationToken);

        return $"{yearPrefix}{number:D8}";
    }

    private async Task<long> NextNumberAsync(
        string key, Func<CancellationToken, Task<long>> seed, CancellationToken cancellationToken)
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
