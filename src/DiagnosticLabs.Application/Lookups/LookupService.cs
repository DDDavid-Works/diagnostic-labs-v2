using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Domain.Settings;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Application.Lookups;

public static class LookupFields
{
    public const string Sex = "Gender";
    public const string CivilStatus = "Civil Status";
}

public interface ILookupService
{
    /// <summary>The dropdown choices stored for a field (legacy "single line entries").</summary>
    Task<IReadOnlyList<string>> GetChoicesAsync(string fieldName, CancellationToken cancellationToken = default);

    /// <summary>Remembers a newly typed choice so it appears in the dropdown next time. Does nothing if it already exists.</summary>
    Task AddChoiceIfNewAsync(string fieldName, string? value, CancellationToken cancellationToken = default);
}

public sealed class LookupService(IAppDbContext db) : ILookupService
{
    public async Task<IReadOnlyList<string>> GetChoicesAsync(string fieldName, CancellationToken cancellationToken = default) =>
        await db.LookupValues
            .AsNoTracking()
            .Where(l => l.Kind == LookupKind.SingleLine && l.FieldName == fieldName && l.IsActive)
            .OrderBy(l => l.Id)
            .Select(l => l.Value)
            .ToListAsync(cancellationToken);

    public async Task AddChoiceIfNewAsync(string fieldName, string? value, CancellationToken cancellationToken = default)
    {
        value = value?.Trim();
        if (string.IsNullOrEmpty(value))
            return;

        var exists = await db.LookupValues.AnyAsync(
            l => l.Kind == LookupKind.SingleLine && l.FieldName == fieldName && l.Value == value, cancellationToken);
        if (exists)
            return;

        db.LookupValues.Add(new LookupValue { Kind = LookupKind.SingleLine, FieldName = fieldName, Value = value });
        await db.SaveChangesAsync(cancellationToken);
    }
}
