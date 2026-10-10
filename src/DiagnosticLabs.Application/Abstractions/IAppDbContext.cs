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
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace DiagnosticLabs.Application.Abstractions;

public interface IAppDbContext
{
    DbSet<User> Users { get; }

    DbSet<Module> Modules { get; }

    DbSet<ModuleType> ModuleTypes { get; }

    DbSet<UserPermission> UserPermissions { get; }

    DbSet<Patient> Patients { get; }

    DbSet<Company> Companies { get; }

    DbSet<PatientRegistration> PatientRegistrations { get; }

    DbSet<PatientRegistrationService> PatientRegistrationServices { get; }

    DbSet<PatientRegistrationDiscountStep> PatientRegistrationDiscountSteps { get; }

    DbSet<Payment> Payments { get; }

    DbSet<Service> Services { get; }

    DbSet<Package> Packages { get; }

    DbSet<PackageService> PackageServices { get; }

    DbSet<Discount> Discounts { get; }

    DbSet<DiscountDetail> DiscountDetails { get; }

    DbSet<Department> Departments { get; }

    DbSet<Item> Items { get; }

    DbSet<ItemLocation> ItemLocations { get; }

    DbSet<ItemQuantity> ItemQuantities { get; }

    DbSet<ServiceItemQuantity> ServiceItemQuantities { get; }

    DbSet<LabReport> LabReports { get; }

    DbSet<LabReportPhoto> LabReportPhotos { get; }

    DbSet<CompanySetup> CompanySetups { get; }

    DbSet<LookupValue> LookupValues { get; }

    DbSet<ModuleDefault> ModuleDefaults { get; }

    DbSet<CodeSequence> CodeSequences { get; }

    DbSet<AuditLog> AuditLogs { get; }

    /// <summary>Any mapped entity set, for the per-type lab result tables.</summary>
    DbSet<TEntity> Set<TEntity>() where TEntity : class;

    ChangeTracker ChangeTracker { get; }

    /// <summary>Makes the next save fail with a concurrency error if the row changed since <paramref name="rowVersion"/> was read.</summary>
    void SetOriginalRowVersion(AuditableEntity entity, byte[] rowVersion);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
