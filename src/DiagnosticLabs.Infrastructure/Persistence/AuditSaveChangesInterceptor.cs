using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace DiagnosticLabs.Infrastructure.Persistence;

/// <summary>
/// Stamps the audit columns (UTC time + current user) and turns deletes of soft-deletable
/// rows into an <c>IsDeleted</c> flag, so no service has to.
/// </summary>
public sealed class AuditSaveChangesInterceptor(ICurrentUser currentUser, IClock clock) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Stamp(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Stamp(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Stamp(DbContext? context)
    {
        if (context is null)
            return;

        var now = clock.UtcNow;
        var userId = currentUser.UserId;

        // Convert deletes first so the rows are stamped as updates below.
        foreach (EntityEntry entry in context.ChangeTracker.Entries().Where(e => e.State == EntityState.Deleted).ToList())
        {
            if (entry.Entity is not ISoftDeletable softDeletable)
                continue;

            entry.State = EntityState.Modified;
            softDeletable.IsDeleted = true;
            softDeletable.DeletedAtUtc = now;
            softDeletable.DeletedByUserId = userId;
        }

        foreach (EntityEntry<AuditableEntity> entry in context.ChangeTracker.Entries<AuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAtUtc = now;
                    entry.Entity.CreatedByUserId = userId;
                    entry.Entity.UpdatedAtUtc = now;
                    entry.Entity.UpdatedByUserId = userId;
                    break;
                case EntityState.Modified:
                    entry.Property(e => e.CreatedAtUtc).IsModified = false;
                    entry.Property(e => e.CreatedByUserId).IsModified = false;
                    entry.Entity.UpdatedAtUtc = now;
                    entry.Entity.UpdatedByUserId = userId;
                    break;
            }
        }
    }
}
