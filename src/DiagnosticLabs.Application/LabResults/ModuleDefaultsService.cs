using System.Text.Json;
using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Domain.Settings;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Application.LabResults;

/// <summary>
/// The values a lab result form starts with when a new record is made ("defaults"). One set per result type, shared by everyone;
/// only administrators can change it. Stored as JSON keyed by field name, so adding or removing a field later never breaks it.
/// </summary>
public interface IModuleDefaultsService
{
    /// <summary>The saved defaults of a result screen (empty when none were set). Needs View on that screen.</summary>
    Task<Result<IReadOnlyDictionary<string, string?>>> GetAsync(int moduleId, CancellationToken cancellationToken = default);

    /// <summary>Replaces the defaults of a result screen. Administrators only.</summary>
    Task<Result> SaveAsync(int moduleId, IReadOnlyDictionary<string, string?> values, CancellationToken cancellationToken = default);

    /// <summary>Removes the defaults of a result screen. Administrators only.</summary>
    Task<Result> ClearAsync(int moduleId, CancellationToken cancellationToken = default);
}

public sealed class ModuleDefaultsService(IAppDbContext db, ICurrentUser currentUser) : IModuleDefaultsService
{
    public const int MaxValueLength = 1000;
    public const int MaxFieldNameLength = 100;

    public async Task<Result<IReadOnlyDictionary<string, string?>>> GetAsync(int moduleId, CancellationToken cancellationToken = default)
    {
        if (!currentUser.Can(moduleId, ModuleAction.View))
            return Result<IReadOnlyDictionary<string, string?>>.Failure(Errors.Forbidden);

        var json = await db.ModuleDefaults.AsNoTracking()
            .Where(d => d.ModuleId == moduleId && d.IsActive)
            .Select(d => d.Defaults)
            .FirstOrDefaultAsync(cancellationToken);

        return Result<IReadOnlyDictionary<string, string?>>.Success(Parse(json));
    }

    public async Task<Result> SaveAsync(int moduleId, IReadOnlyDictionary<string, string?> values, CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAdmin)
            return Result.Failure(Errors.Forbidden);

        var clean = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var (name, value) in values)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Length > MaxFieldNameLength)
                return Result.Failure(Errors.Invalid(["A default has an invalid field name."]));

            var trimmed = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            if (trimmed?.Length > MaxValueLength)
                return Result.Failure(Errors.Invalid([$"The default for {name} can not be longer than {MaxValueLength} characters."]));

            if (trimmed is not null)
                clean[name] = trimmed;
        }

        if (!await db.Modules.AnyAsync(m => m.Id == moduleId, cancellationToken))
            return Result.Failure(Errors.NotFound("Module"));

        var json = JsonSerializer.Serialize(clean);
        var row = await db.ModuleDefaults.FirstOrDefaultAsync(d => d.ModuleId == moduleId, cancellationToken);
        if (row is null)
            db.ModuleDefaults.Add(new ModuleDefault { ModuleId = moduleId, Defaults = json });
        else
        {
            row.Defaults = json;
            row.IsActive = true;
        }

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> ClearAsync(int moduleId, CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAdmin)
            return Result.Failure(Errors.Forbidden);

        var row = await db.ModuleDefaults.FirstOrDefaultAsync(d => d.ModuleId == moduleId, cancellationToken);
        if (row is not null)
        {
            db.ModuleDefaults.Remove(row);
            await db.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }

    // A damaged or unreadable entry just means "no defaults"; it must never stop a new result from being made.
    private static IReadOnlyDictionary<string, string?> Parse(string? json)
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