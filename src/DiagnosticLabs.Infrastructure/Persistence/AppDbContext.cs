using System.Linq.Expressions;
using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Domain.Auditing;
using DiagnosticLabs.Domain.Billing;
using DiagnosticLabs.Domain.Catalog;
using DiagnosticLabs.Domain.Common;
using DiagnosticLabs.Domain.Identity;
using DiagnosticLabs.Domain.Inventory;
using DiagnosticLabs.Domain.Lab;
using DiagnosticLabs.Domain.Patients;
using DiagnosticLabs.Domain.Registrations;
using DiagnosticLabs.Domain.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace DiagnosticLabs.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<User> Users => Set<User>();

    public DbSet<Module> Modules => Set<Module>();

    public DbSet<ModuleType> ModuleTypes => Set<ModuleType>();

    public DbSet<UserPermission> UserPermissions => Set<UserPermission>();

    public DbSet<Patient> Patients => Set<Patient>();

    public DbSet<Company> Companies => Set<Company>();

    public DbSet<PatientRegistration> PatientRegistrations => Set<PatientRegistration>();

    public DbSet<PatientRegistrationService> PatientRegistrationServices => Set<PatientRegistrationService>();

    public DbSet<Payment> Payments => Set<Payment>();

    public DbSet<Service> Services => Set<Service>();

    public DbSet<Package> Packages => Set<Package>();

    public DbSet<PackageService> PackageServices => Set<PackageService>();

    public DbSet<Discount> Discounts => Set<Discount>();

    public DbSet<DiscountDetail> DiscountDetails => Set<DiscountDetail>();

    public DbSet<Department> Departments => Set<Department>();

    public DbSet<Item> Items => Set<Item>();

    public DbSet<ItemLocation> ItemLocations => Set<ItemLocation>();

    public DbSet<ItemQuantity> ItemQuantities => Set<ItemQuantity>();

    public DbSet<ServiceItemQuantity> ServiceItemQuantities => Set<ServiceItemQuantity>();

    public DbSet<LabReport> LabReports => Set<LabReport>();

    public DbSet<LabReportPhoto> LabReportPhotos => Set<LabReportPhoto>();

    public DbSet<CompanySetup> CompanySetups => Set<CompanySetup>();

    public DbSet<LookupValue> LookupValues => Set<LookupValue>();

    public DbSet<ModuleDefault> ModuleDefaults => Set<ModuleDefault>();

    public DbSet<CodeSequence> CodeSequences => Set<CodeSequence>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public void SetOriginalRowVersion(AuditableEntity entity, byte[] rowVersion) =>
        Entry(entity).Property(e => e.RowVersion).OriginalValue = rowVersion;

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HaveColumnType("datetime2(3)");
        configurationBuilder.Properties<decimal>().HavePrecision(18, 4);
        configurationBuilder.Conventions.Remove(typeof(ForeignKeyIndexConvention));
        configurationBuilder.Conventions.Add(_ => new SelectiveForeignKeyIndexConvention());
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        modelBuilder.Ignore<LabReportDetail>();

        foreach (var entityType in modelBuilder.Model.GetEntityTypes().ToList())
        {
            var clrType = entityType.ClrType;

            if (typeof(AuditableEntity).IsAssignableFrom(clrType))
                ConfigureAudit(modelBuilder, entityType);

            if (typeof(LabReportDetail).IsAssignableFrom(clrType))
                HideDetailsOfDeletedReports(modelBuilder, clrType);

            if (typeof(ISoftDeletable).IsAssignableFrom(clrType))
                ConfigureSoftDelete(modelBuilder, entityType);
        }

        // Nothing is ever cascade-deleted by the database; lab detail rows and photos follow their header.
        foreach (var foreignKey in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
        {
            var dependent = foreignKey.DeclaringEntityType.ClrType;
            var cascades = foreignKey.PrincipalEntityType.ClrType == typeof(LabReport)
                           && (typeof(LabReportDetail).IsAssignableFrom(dependent) || dependent == typeof(LabReportPhoto));
            foreignKey.DeleteBehavior = cascades ? DeleteBehavior.Cascade : DeleteBehavior.Restrict;
        }
    }

    private static void ConfigureAudit(ModelBuilder modelBuilder, IMutableEntityType entityType)
    {
        var builder = modelBuilder.Entity(entityType.ClrType);
        builder.Property(nameof(AuditableEntity.RowVersion)).IsRowVersion();
        builder.Property(nameof(AuditableEntity.CreatedAtUtc)).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(nameof(AuditableEntity.UpdatedAtUtc)).HasDefaultValueSql("SYSUTCDATETIME()");

        foreach (var column in new[] { nameof(AuditableEntity.CreatedByUserId), nameof(AuditableEntity.UpdatedByUserId) })
            AddUserForeignKey(builder, column);
    }

    private static void ConfigureSoftDelete(ModelBuilder modelBuilder, IMutableEntityType entityType)
    {
        var builder = modelBuilder.Entity(entityType.ClrType);
        AddUserForeignKey(builder, nameof(ISoftDeletable.DeletedByUserId));

        // Soft-deleted rows are invisible unless a query opts out with IgnoreQueryFilters().
        var parameter = Expression.Parameter(entityType.ClrType, "e");
        var isDeleted = Expression.Call(
            typeof(EF), nameof(EF.Property), [typeof(bool)], parameter, Expression.Constant(nameof(ISoftDeletable.IsDeleted)));
        builder.HasQueryFilter(Expression.Lambda(Expression.Not(isDeleted), parameter));
    }

    // A lab result row follows its header: when the report is soft-deleted its details disappear too.
    private static void HideDetailsOfDeletedReports(ModelBuilder modelBuilder, Type detailType)
    {
        var parameter = Expression.Parameter(detailType, "d");
        var report = Expression.Property(parameter, nameof(LabReportDetail.LabReport));
        var isDeleted = Expression.Property(report, nameof(LabReport.IsDeleted));
        modelBuilder.Entity(detailType).HasQueryFilter(Expression.Lambda(Expression.Not(isDeleted), parameter));
    }

    private static void AddUserForeignKey(
        Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder builder, string column) =>
        builder.HasOne(typeof(User)).WithMany().HasForeignKey(column).OnDelete(DeleteBehavior.Restrict);
}
