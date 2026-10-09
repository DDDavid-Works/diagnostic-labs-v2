using DiagnosticLabs.Application.Auth;
using DiagnosticLabs.Domain.Common;
using DiagnosticLabs.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace DiagnosticLabs.Application.Tests.Persistence;

public class ModelConventionTests
{
    private static IModel BuildModel()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=none;Database=none")
            .Options;
        using var db = new AppDbContext(options);
        return db.GetService<IDesignTimeModel>().Model;
    }

    [Fact]
    public void Every_auditable_table_has_the_standard_columns()
    {
        var model = BuildModel();

        foreach (var entity in model.GetEntityTypes().Where(e => typeof(AuditableEntity).IsAssignableFrom(e.ClrType)))
        {
            Assert.NotNull(entity.FindProperty(nameof(AuditableEntity.CreatedAtUtc)));
            Assert.True(entity.FindProperty(nameof(AuditableEntity.RowVersion))!.IsConcurrencyToken, entity.Name);
            Assert.True(entity.FindProperty(nameof(AuditableEntity.CreatedByUserId))!.IsNullable, entity.Name);
        }
    }

    [Fact]
    public void Soft_deletable_tables_have_a_query_filter()
    {
        var model = BuildModel();

        foreach (var entity in model.GetEntityTypes().Where(e => typeof(ISoftDeletable).IsAssignableFrom(e.ClrType)))
            Assert.NotEmpty(entity.GetDeclaredQueryFilters());
    }

    [Fact]
    public void Audit_user_columns_are_foreign_keys_but_not_indexed()
    {
        var model = BuildModel();
        var audit = new[] { "CreatedByUserId", "UpdatedByUserId", "DeletedByUserId" };

        foreach (var entity in model.GetEntityTypes().Where(e => typeof(AuditableEntity).IsAssignableFrom(e.ClrType)))
        {
            Assert.Contains(entity.GetForeignKeys(), fk => fk.Properties[0].Name == "CreatedByUserId");
            Assert.DoesNotContain(entity.GetIndexes(), i => i.Properties.Count == 1 && audit.Contains(i.Properties[0].Name));
        }
    }

    [Fact]
    public void Session_type_is_usable_for_design_time_construction()
    {
        // Guards the DesignTimeDbContextFactory's assumption that a bare session needs no setup.
        Assert.False(new CurrentUserSession().IsAuthenticated);
    }
}
