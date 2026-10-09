using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Domain.Settings;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Application.Entries;

public enum EntryKind
{
    /// <summary>A dropdown choice (a short piece of text).</summary>
    SingleLine,

    /// <summary>A reusable text template: a title to pick it by and a block of text.</summary>
    MultiLine,
}

/// <summary>
/// Identifies one maintained list of entries. <see cref="ModuleId"/> is <c>null</c> for general lists shared by
/// several screens (Gender, Medical Technologist...) and the module's id for lists that belong to one screen.
/// </summary>
public sealed record EntryField(string Name, EntryKind Kind, int? ModuleId = null);

/// <summary>Every list the app maintains through the entry builder, declared once.</summary>
public static class EntryFields
{
    public static readonly EntryField Gender = new("Gender", EntryKind.SingleLine);
    public static readonly EntryField CivilStatus = new("Civil Status", EntryKind.SingleLine);
    public static readonly EntryField MedicalTechnologist = new("Medical Technologist", EntryKind.SingleLine);
    public static readonly EntryField Pathologist = new("Pathologist", EntryKind.SingleLine);

    // Lists that belong to one result screen (the legacy screens kept them per module as well).
    public static readonly EntryField StoolColor = new("Color", EntryKind.SingleLine, ModuleIds.StoolFecalysis);
    public static readonly EntryField StoolConsistency = new("Consistency", EntryKind.SingleLine, ModuleIds.StoolFecalysis);
    public static readonly EntryField StoolResult = new("Result", EntryKind.MultiLine, ModuleIds.StoolFecalysis);
    public static readonly EntryField StoolRemarks = new("Remarks", EntryKind.MultiLine, ModuleIds.StoolFecalysis);
}

public sealed record SingleLineEntry(long Id, string Value);

public sealed record MultiLineEntry(long Id, string Title, string Text);

public sealed record SingleLineEntryList(string FieldName, string ScopeName, IReadOnlyList<SingleLineEntry> Items);

public sealed record MultiLineEntryList(string FieldName, string ScopeName, IReadOnlyList<MultiLineEntry> Items);

public interface IEntryService
{
    /// <summary>The dropdown values of a single-line list, in the order they were added.</summary>
    Task<IReadOnlyList<string>> GetChoicesAsync(EntryField field, CancellationToken cancellationToken = default);

    Task<Result<SingleLineEntryList>> GetSingleLineAsync(EntryField field, int hostModuleId, CancellationToken cancellationToken = default);

    /// <summary>Replaces a list with the submitted rows: rows with an id are renamed, new rows are added, missing rows are removed.</summary>
    Task<Result<SingleLineEntryList>> SaveSingleLineAsync(
        EntryField field, int hostModuleId, IReadOnlyList<SingleLineEntry> items, CancellationToken cancellationToken = default);

    Task<Result<MultiLineEntryList>> GetMultiLineAsync(EntryField field, int hostModuleId, CancellationToken cancellationToken = default);

    Task<Result<MultiLineEntryList>> SaveMultiLineAsync(
        EntryField field, int hostModuleId, IReadOnlyList<MultiLineEntry> items, CancellationToken cancellationToken = default);
}

/// <summary>
/// The one place that maintains every dropdown / template list, so no screen needs its own maintenance window.
/// Order is the order of adding (by id): renaming keeps a row where it is. Removing a row switches it off.
/// </summary>
public sealed class EntryService(IAppDbContext db, ICurrentUser currentUser) : IEntryService
{
    public const int ValueMaxLength = 200;
    public const int TitleMaxLength = 100;

    public async Task<IReadOnlyList<string>> GetChoicesAsync(EntryField field, CancellationToken cancellationToken = default) =>
        await Query(field, LookupKind.SingleLine)
            .AsNoTracking()
            .Select(l => l.Value)
            .ToListAsync(cancellationToken);

    public async Task<Result<SingleLineEntryList>> GetSingleLineAsync(
        EntryField field, int hostModuleId, CancellationToken cancellationToken = default)
    {
        if (Reject(field, EntryKind.SingleLine, hostModuleId, ModuleAction.View) is { } error)
            return Result<SingleLineEntryList>.Failure(error);

        return Result<SingleLineEntryList>.Success(await LoadSingleAsync(field, cancellationToken));
    }

    public async Task<Result<SingleLineEntryList>> SaveSingleLineAsync(
        EntryField field, int hostModuleId, IReadOnlyList<SingleLineEntry> items, CancellationToken cancellationToken = default)
    {
        if (Reject(field, EntryKind.SingleLine, hostModuleId, ModuleAction.Edit) is { } error)
            return Result<SingleLineEntryList>.Failure(error);

        var errors = new List<string>();
        foreach (var item in items)
        {
            var value = item.Value?.Trim() ?? string.Empty;
            if (value.Length == 0)
                errors.Add("An entry can not be empty.");
            else if (value.Length > ValueMaxLength)
                errors.Add($"An entry can not be longer than {ValueMaxLength} characters.");
        }

        AddDuplicateError(errors, items.Select(i => i.Value), "entry");
        if (errors.Count > 0)
            return Result<SingleLineEntryList>.Failure(Errors.Invalid(errors.Distinct()));

        var existing = await Query(field, LookupKind.SingleLine).ToListAsync(cancellationToken);
        Sync(existing, items, i => i.Id, e => e.Id, (e, i) => e.Value = i.Value.Trim(), () => New(field, LookupKind.SingleLine));
        await db.SaveChangesAsync(cancellationToken);

        return Result<SingleLineEntryList>.Success(await LoadSingleAsync(field, cancellationToken));
    }

    public async Task<Result<MultiLineEntryList>> GetMultiLineAsync(
        EntryField field, int hostModuleId, CancellationToken cancellationToken = default)
    {
        if (Reject(field, EntryKind.MultiLine, hostModuleId, ModuleAction.View) is { } error)
            return Result<MultiLineEntryList>.Failure(error);

        return Result<MultiLineEntryList>.Success(await LoadMultiAsync(field, cancellationToken));
    }

    public async Task<Result<MultiLineEntryList>> SaveMultiLineAsync(
        EntryField field, int hostModuleId, IReadOnlyList<MultiLineEntry> items, CancellationToken cancellationToken = default)
    {
        if (Reject(field, EntryKind.MultiLine, hostModuleId, ModuleAction.Edit) is { } error)
            return Result<MultiLineEntryList>.Failure(error);

        var errors = new List<string>();
        foreach (var item in items)
        {
            var title = item.Title?.Trim() ?? string.Empty;
            if (title.Length == 0)
                errors.Add("An entry needs a name.");
            else if (title.Length > TitleMaxLength)
                errors.Add($"An entry name can not be longer than {TitleMaxLength} characters.");
        }

        AddDuplicateError(errors, items.Select(i => i.Title), "entry name");
        if (errors.Count > 0)
            return Result<MultiLineEntryList>.Failure(Errors.Invalid(errors.Distinct()));

        var existing = await Query(field, LookupKind.MultiLine).ToListAsync(cancellationToken);
        Sync(
            existing,
            items,
            i => i.Id,
            e => e.Id,
            (e, i) =>
            {
                e.Title = i.Title.Trim();
                e.Value = i.Text ?? string.Empty;
            },
            () => New(field, LookupKind.MultiLine));
        await db.SaveChangesAsync(cancellationToken);

        return Result<MultiLineEntryList>.Success(await LoadMultiAsync(field, cancellationToken));
    }

    // ---------------------------------------------------------------- helpers

    private IQueryable<LookupValue> Query(EntryField field, LookupKind kind) =>
        db.LookupValues
            .Where(l => l.Kind == kind && l.FieldName == field.Name && l.ModuleId == field.ModuleId && l.IsActive)
            .OrderBy(l => l.Id);

    private async Task<SingleLineEntryList> LoadSingleAsync(EntryField field, CancellationToken cancellationToken)
    {
        var rows = await Query(field, LookupKind.SingleLine).AsNoTracking().ToListAsync(cancellationToken);
        return new SingleLineEntryList(field.Name, await ScopeNameAsync(field, cancellationToken), [.. rows.Select(r => new SingleLineEntry(r.Id, r.Value))]);
    }

    private async Task<MultiLineEntryList> LoadMultiAsync(EntryField field, CancellationToken cancellationToken)
    {
        var rows = await Query(field, LookupKind.MultiLine).AsNoTracking().ToListAsync(cancellationToken);
        return new MultiLineEntryList(
            field.Name, await ScopeNameAsync(field, cancellationToken), [.. rows.Select(r => new MultiLineEntry(r.Id, r.Title ?? string.Empty, r.Value))]);
    }

    private async Task<string> ScopeNameAsync(EntryField field, CancellationToken cancellationToken)
    {
        if (field.ModuleId is not { } moduleId)
            return "General list (shared by several screens)";

        return await db.Modules.Where(m => m.Id == moduleId).Select(m => m.ModuleName).FirstOrDefaultAsync(cancellationToken)
               ?? $"Module {moduleId}";
    }

    private Error? Reject(EntryField field, EntryKind expected, int hostModuleId, ModuleAction action)
    {
        if (field.Kind != expected)
            return new Error("Entries.WrongKind", $"'{field.Name}' is not a {(expected == EntryKind.SingleLine ? "single-line" : "multi-line")} list.");

        return currentUser.Can(hostModuleId, action) ? null : Errors.Forbidden;
    }

    private static LookupValue New(EntryField field, LookupKind kind) =>
        new() { Kind = kind, FieldName = field.Name, ModuleId = field.ModuleId, Value = string.Empty };

    private static void AddDuplicateError(List<string> errors, IEnumerable<string?> values, string what)
    {
        var duplicated = values
            .Select(v => v?.Trim() ?? string.Empty)
            .Where(v => v.Length > 0)
            .GroupBy(v => v, StringComparer.OrdinalIgnoreCase)
            .Any(g => g.Count() > 1);

        if (duplicated)
            errors.Add($"Each {what} can only be listed once.");
    }

    /// <summary>Rows with a known id are updated, rows with id 0 are added, rows no longer submitted are switched off.</summary>
    private void Sync<TItem>(
        List<LookupValue> existing,
        IReadOnlyList<TItem> items,
        Func<TItem, long> itemId,
        Func<LookupValue, long> rowId,
        Action<LookupValue, TItem> copy,
        Func<LookupValue> create)
    {
        var submittedIds = items.Select(itemId).Where(id => id != 0).ToHashSet();

        foreach (var row in existing.Where(r => !submittedIds.Contains(rowId(r))))
            row.IsActive = false;

        foreach (var item in items)
        {
            var row = itemId(item) == 0 ? null : existing.FirstOrDefault(r => rowId(r) == itemId(item));
            if (row is null)
            {
                row = create();
                db.LookupValues.Add(row);
            }

            copy(row, item);
        }
    }
}
