using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace DiagnosticLabs.Infrastructure.Persistence;

/// <summary>
/// Replaces EF's "index every foreign key" convention. The Created/Updated/Deleted-by columns
/// reference Users for integrity only and are never searched on, so indexing them would be pure
/// write overhead; every other foreign key still gets an index unless a key or index already covers it.
/// </summary>
internal sealed class SelectiveForeignKeyIndexConvention : IModelFinalizingConvention
{
    private static readonly HashSet<string> AuditColumns = ["CreatedByUserId", "UpdatedByUserId", "DeletedByUserId"];

    public void ProcessModelFinalizing(IConventionModelBuilder modelBuilder, IConventionContext<IConventionModelBuilder> context)
    {
        foreach (var entityType in modelBuilder.Metadata.GetEntityTypes())
        {
            foreach (var foreignKey in entityType.GetDeclaredForeignKeys())
            {
                if (foreignKey.Properties.Count == 1 && AuditColumns.Contains(foreignKey.Properties[0].Name))
                    continue;

                if (!IsCovered(entityType, foreignKey))
                    entityType.AddIndex(foreignKey.Properties);
            }
        }
    }

    private static bool IsCovered(IConventionEntityType entityType, IConventionForeignKey foreignKey) =>
        entityType.GetKeys().Any(k => StartsWith(k.Properties, foreignKey.Properties))
        || entityType.GetIndexes().Any(i => StartsWith(i.Properties, foreignKey.Properties));

    private static bool StartsWith(IReadOnlyList<IConventionProperty> candidate, IReadOnlyList<IConventionProperty> prefix) =>
        candidate.Count >= prefix.Count && prefix.Select((p, i) => candidate[i] == p).All(match => match);
}
