using System.Text.Json;
using System.Text.Json.Serialization;
using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Domain.Auditing;
using DiagnosticLabs.Domain.Common;
using DiagnosticLabs.Domain.Lab;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;

namespace DiagnosticLabs.Infrastructure.Persistence;

/// <summary>
/// Writes one <see cref="AuditLog"/> row per inserted/updated/deleted business row, in the
/// same transaction as the change. Must run after <see cref="AuditSaveChangesInterceptor"/>
/// (register it second) so soft deletes are already visible as updates.
/// </summary>
public sealed class AuditLogInterceptor(ICurrentUser currentUser, IClock clock) : SaveChangesInterceptor
{
    private static readonly JsonSerializerOptions Json = new()
    {
        Converters = { new JsonStringEnumConverter() },
        WriteIndented = false,
    };

    // Changes to these are bookkeeping, not business changes.
    private static readonly HashSet<string> NoiseProperties =
        ["RowVersion", "UpdatedAtUtc", "UpdatedByUserId", "CreatedAtUtc", "CreatedByUserId"];

    private static readonly HashSet<string> SecretProperties = ["PasswordHash"];

    private List<PendingEntry> _pending = [];
    private IDbContextTransaction? _ownedTransaction;
    private bool _writing;

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Capture(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Capture(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        if (!_writing && eventData.Context is { } context && _pending.Count > 0)
        {
            _writing = true;
            try
            {
                WriteLogs(context);
                context.SaveChanges();
                CommitOwned();
            }
            finally
            {
                _writing = false;
                _pending = [];
            }
        }

        return base.SavedChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        if (!_writing && eventData.Context is { } context && _pending.Count > 0)
        {
            _writing = true;
            try
            {
                WriteLogs(context);
                await context.SaveChangesAsync(cancellationToken);
                if (_ownedTransaction is not null)
                {
                    await _ownedTransaction.CommitAsync(cancellationToken);
                    await _ownedTransaction.DisposeAsync();
                    _ownedTransaction = null;
                }
            }
            finally
            {
                _writing = false;
                _pending = [];
            }
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData) => Abort();

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        Abort();
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    private void Capture(DbContext? context)
    {
        if (_writing || context is null)
            return;

        _pending = [];
        foreach (EntityEntry entry in context.ChangeTracker.Entries().Where(IsAudited))
        {
            var pending = entry.State switch
            {
                EntityState.Added => new PendingEntry(entry, AuditAction.Insert, null, Values(entry, p => p.CurrentValue)),
                EntityState.Deleted => new PendingEntry(entry, AuditAction.Delete, Values(entry, p => p.OriginalValue), null),
                EntityState.Modified => CaptureUpdate(entry),
                _ => null,
            };

            if (pending is not null)
                _pending.Add(pending);
        }

        // Make the data change and its audit rows atomic when the caller has not opened a transaction.
        if (_pending.Count > 0 && context.Database.CurrentTransaction is null && context.Database.IsRelational())
            _ownedTransaction = context.Database.BeginTransaction();
    }

    private static PendingEntry? CaptureUpdate(EntityEntry entry)
    {
        var changed = entry.Properties
            .Where(p => p.IsModified && !NoiseProperties.Contains(p.Metadata.Name)
                        && !Equals(p.OriginalValue, p.CurrentValue))
            .ToList();

        if (changed.Count == 0)
            return null;

        var old = new Dictionary<string, object?>();
        var current = new Dictionary<string, object?>();
        foreach (var property in changed)
        {
            old[property.Metadata.Name] = Render(property, property.OriginalValue);
            current[property.Metadata.Name] = Render(property, property.CurrentValue);
        }

        // A soft delete is stored as an update of IsDeleted; report it as what it means.
        var action = entry.Entity is ISoftDeletable { IsDeleted: true } && changed.Any(p => p.Metadata.Name == nameof(ISoftDeletable.IsDeleted))
            ? AuditAction.Delete
            : AuditAction.Update;

        return new PendingEntry(entry, action, old, current);
    }

    private void WriteLogs(DbContext context)
    {
        var now = clock.UtcNow;
        var set = context.Set<AuditLog>();
        foreach (var item in _pending)
        {
            set.Add(new AuditLog
            {
                EntityName = item.Entry.Metadata.ClrType.Name,
                EntityId = KeyOf(item.Entry),
                Action = item.Action,
                ChangedByUserId = currentUser.UserId,
                ChangedAtUtc = now,
                OldValues = item.Old is null ? null : JsonSerializer.Serialize(item.Old, Json),
                NewValues = item.New is null ? null : JsonSerializer.Serialize(item.New, Json),
            });
        }
    }

    private void CommitOwned()
    {
        _ownedTransaction?.Commit();
        _ownedTransaction?.Dispose();
        _ownedTransaction = null;
    }

    private void Abort()
    {
        _pending = [];
        _ownedTransaction?.Rollback();
        _ownedTransaction?.Dispose();
        _ownedTransaction = null;
    }

    private static bool IsAudited(EntityEntry entry) =>
        entry.Entity is AuditableEntity or LabReportDetail or Domain.Lab.LabReportPhoto;

    private static Dictionary<string, object?> Values(EntityEntry entry, Func<PropertyEntry, object?> pick) =>
        entry.Properties
            .Where(p => !NoiseProperties.Contains(p.Metadata.Name))
            .ToDictionary(p => p.Metadata.Name, p => Render(p, pick(p)));

    private static object? Render(PropertyEntry property, object? value)
    {
        if (SecretProperties.Contains(property.Metadata.Name))
            return "[redacted]";

        return value is byte[] bytes ? $"[binary {bytes.Length} bytes]" : value;
    }

    private static string KeyOf(EntityEntry entry) =>
        string.Join('|', entry.Metadata.FindPrimaryKey()!.Properties.Select(p => entry.Property(p.Name).CurrentValue));

    private sealed record PendingEntry(
        EntityEntry Entry,
        AuditAction Action,
        Dictionary<string, object?>? Old,
        Dictionary<string, object?>? New);
}
