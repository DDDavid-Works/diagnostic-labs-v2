using DiagnosticLabs.Domain.Auditing;
using DiagnosticLabs.Domain.Billing;
using DiagnosticLabs.Domain.Catalog;
using DiagnosticLabs.Domain.Inventory;
using DiagnosticLabs.Domain.Lab;
using DiagnosticLabs.Domain.Patients;
using DiagnosticLabs.Domain.Registrations;
using DiagnosticLabs.Domain.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiagnosticLabs.Infrastructure.Persistence.Configurations;

// Filter used by the unique/lookup indexes so soft-deleted rows do not block re-use of a code.
internal static class Filters
{
    public const string NotDeleted = "[IsDeleted] = 0";
}

internal sealed class PatientConfiguration : IEntityTypeConfiguration<Patient>
{
    public void Configure(EntityTypeBuilder<Patient> builder)
    {
        builder.Property(p => p.PatientCode).HasMaxLength(200);
        builder.Property(p => p.PatientName).HasMaxLength(200);
        builder.Property(p => p.Age).HasMaxLength(50);
        builder.Property(p => p.Sex).HasMaxLength(20);
        builder.Property(p => p.CivilStatus).HasMaxLength(20);
        builder.Property(p => p.Address).HasMaxLength(500);
        builder.Property(p => p.ContactNumbers).HasMaxLength(50);
        builder.HasIndex(p => p.PatientCode).IsUnique().HasFilter(Filters.NotDeleted);
        builder.HasIndex(p => p.PatientName);
    }
}

internal sealed class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.Property(c => c.CompanyName).HasMaxLength(200);
        builder.Property(c => c.Address).HasMaxLength(500);
        builder.Property(c => c.ContactNumbers).HasMaxLength(200);
        builder.Property(c => c.ContactPerson).HasMaxLength(200);
        builder.HasIndex(c => c.CompanyName);
    }
}

internal sealed class ServiceConfiguration : IEntityTypeConfiguration<Service>
{
    public void Configure(EntityTypeBuilder<Service> builder)
    {
        builder.Property(s => s.ServiceName).HasMaxLength(50);
        builder.Property(s => s.ServiceDescription).HasMaxLength(200);
        builder.ToTable(t => t.HasCheckConstraint("CK_Services_Price", "[Price] >= 0"));
        builder.HasIndex(s => s.ServiceName);
    }
}

internal sealed class PackageConfiguration : IEntityTypeConfiguration<Package>
{
    public void Configure(EntityTypeBuilder<Package> builder)
    {
        builder.Property(p => p.PackageName).HasMaxLength(50);
        builder.Property(p => p.PackageDescription).HasMaxLength(200);
        builder.ToTable(t => t.HasCheckConstraint("CK_Packages_Price", "[Price] >= 0"));
        builder.HasOne(p => p.Company).WithMany().HasForeignKey(p => p.CompanyId);
        builder.HasMany(p => p.Services).WithOne(s => s.Package).HasForeignKey(s => s.PackageId);
    }
}

internal sealed class PackageServiceConfiguration : IEntityTypeConfiguration<PackageService>
{
    public void Configure(EntityTypeBuilder<PackageService> builder)
    {
        builder.ToTable(t => t.HasCheckConstraint("CK_PackageServices_Price", "[Price] >= 0"));
        builder.HasOne(p => p.Service).WithMany().HasForeignKey(p => p.ServiceId);
        builder.HasIndex(p => new { p.PackageId, p.ServiceId }).IsUnique().HasFilter(Filters.NotDeleted);
    }
}

internal sealed class DiscountConfiguration : IEntityTypeConfiguration<Discount>
{
    public void Configure(EntityTypeBuilder<Discount> builder)
    {
        builder.Property(d => d.DiscountName).HasMaxLength(50);
        builder.Property(d => d.DiscountDescription).HasMaxLength(200);
        builder.HasMany(d => d.Details).WithOne(x => x.Discount).HasForeignKey(x => x.DiscountId);
    }
}

internal sealed class DiscountDetailConfiguration : IEntityTypeConfiguration<DiscountDetail>
{
    public void Configure(EntityTypeBuilder<DiscountDetail> builder)
    {
        builder.ToTable(t => t.HasCheckConstraint(
            "CK_DiscountDetails_AmountOrPercentage",
            "([Amount] IS NULL AND [Percentage] IS NOT NULL) OR ([Amount] IS NOT NULL AND [Percentage] IS NULL)"));
        builder.ToTable(t => t.HasCheckConstraint("CK_DiscountDetails_Values", "([Amount] IS NULL OR [Amount] >= 0) AND ([Percentage] IS NULL OR [Percentage] BETWEEN 0 AND 100)"));
    }
}

internal sealed class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> builder)
    {
        builder.Property(d => d.DepartmentName).HasMaxLength(50);
        builder.Property(d => d.DepartmentDescription).HasMaxLength(200);
    }
}

internal sealed class ItemConfiguration : IEntityTypeConfiguration<Item>
{
    public void Configure(EntityTypeBuilder<Item> builder)
    {
        builder.Property(i => i.ItemName).HasMaxLength(50);
        builder.ToTable(t => t.HasCheckConstraint("CK_Items_Cost", "[Cost] >= 0"));
    }
}

internal sealed class ItemLocationConfiguration : IEntityTypeConfiguration<ItemLocation>
{
    public void Configure(EntityTypeBuilder<ItemLocation> builder) =>
        builder.Property(i => i.ItemLocationName).HasMaxLength(50);
}

internal sealed class ItemQuantityConfiguration : IEntityTypeConfiguration<ItemQuantity>
{
    public void Configure(EntityTypeBuilder<ItemQuantity> builder)
    {
        builder.ToTable(t => t.HasCheckConstraint("CK_ItemQuantities_Quantity", "[Quantity] >= 0"));
        builder.HasOne(i => i.Item).WithMany(i => i.Quantities).HasForeignKey(i => i.ItemId);
        builder.HasOne(i => i.ItemLocation).WithMany().HasForeignKey(i => i.ItemLocationId);
        builder.HasIndex(i => new { i.ItemId, i.ItemLocationId }).IsUnique().HasFilter(Filters.NotDeleted);
    }
}

internal sealed class ServiceItemQuantityConfiguration : IEntityTypeConfiguration<ServiceItemQuantity>
{
    public void Configure(EntityTypeBuilder<ServiceItemQuantity> builder)
    {
        builder.ToTable(t => t.HasCheckConstraint("CK_ServiceItemQuantities_Quantity", "[Quantity] >= 0"));
        builder.HasOne(i => i.Service).WithMany(s => s.Items).HasForeignKey(i => i.ServiceId);
        builder.HasOne(i => i.Item).WithMany().HasForeignKey(i => i.ItemId);
        builder.HasIndex(i => new { i.ServiceId, i.ItemId }).IsUnique().HasFilter(Filters.NotDeleted);
    }
}

internal sealed class PatientRegistrationConfiguration : IEntityTypeConfiguration<PatientRegistration>
{
    public void Configure(EntityTypeBuilder<PatientRegistration> builder)
    {
        builder.Property(r => r.RegistrationCode).HasMaxLength(100);
        builder.Property(r => r.BatchName).HasMaxLength(200);
        builder.ToTable(t => t.HasCheckConstraint("CK_PatientRegistrations_Amounts", "[AmountDue] >= 0 AND [DiscountTotal] >= 0"));
        builder.ToTable(t => t.HasCheckConstraint(
            "CK_PatientRegistrations_DiscountPercentage",
            "[DiscountPercentage] IS NULL OR [DiscountPercentage] BETWEEN 0 AND 100"));
        builder.HasOne(r => r.Patient).WithMany().HasForeignKey(r => r.PatientId);
        builder.HasOne(r => r.Company).WithMany().HasForeignKey(r => r.CompanyId);
        builder.HasOne(r => r.Package).WithMany().HasForeignKey(r => r.PackageId);
        builder.HasOne(r => r.Discount).WithMany().HasForeignKey(r => r.DiscountId);
        builder.HasMany(r => r.Services).WithOne(s => s.PatientRegistration).HasForeignKey(s => s.PatientRegistrationId);
        builder.HasMany(r => r.Payments).WithOne(p => p.PatientRegistration).HasForeignKey(p => p.PatientRegistrationId);
        builder.HasIndex(r => r.RegistrationCode).IsUnique().HasFilter(Filters.NotDeleted);
        builder.HasIndex(r => r.InputDate);
    }
}

internal sealed class PatientRegistrationServiceConfiguration : IEntityTypeConfiguration<PatientRegistrationService>
{
    public void Configure(EntityTypeBuilder<PatientRegistrationService> builder)
    {
        builder.ToTable(t => t.HasCheckConstraint("CK_PatientRegistrationServices_Price", "[Price] >= 0"));
        builder.HasOne(s => s.Service).WithMany().HasForeignKey(s => s.ServiceId);
    }
}

internal sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.Property(p => p.Type).HasConversion<int>();
        builder.ToTable(t => t.HasCheckConstraint("CK_Payments_Amount", "[PaymentAmount] > 0 OR ([Type] = 1 AND [PaymentAmount] >= 0)"));
        builder.HasIndex(p => p.PaymentDate);
    }
}

internal sealed class CompanySetupConfiguration : IEntityTypeConfiguration<CompanySetup>
{
    public void Configure(EntityTypeBuilder<CompanySetup> builder)
    {
        builder.Property(c => c.CompanyName).HasMaxLength(200);
        builder.Property(c => c.SubCompanyName).HasMaxLength(200);
        builder.Property(c => c.Tagline).HasMaxLength(500);
        builder.Property(c => c.Address).HasMaxLength(200);
        builder.Property(c => c.ContactNumbers).HasMaxLength(200);
        builder.Property(c => c.Email).HasMaxLength(200);
        builder.Property(c => c.Code).HasMaxLength(20);
    }
}

internal sealed class LookupValueConfiguration : IEntityTypeConfiguration<LookupValue>
{
    public void Configure(EntityTypeBuilder<LookupValue> builder)
    {
        builder.Property(l => l.Kind).HasConversion<int>();
        builder.Property(l => l.FieldName).HasMaxLength(50);
        builder.Property(l => l.Title).HasMaxLength(100);
        builder.HasOne<Domain.Identity.Module>().WithMany().HasForeignKey(l => l.ModuleId);
        builder.HasIndex(l => new { l.ModuleId, l.Kind, l.FieldName });
    }
}

internal sealed class ModuleDefaultConfiguration : IEntityTypeConfiguration<ModuleDefault>
{
    public void Configure(EntityTypeBuilder<ModuleDefault> builder) =>
        builder.HasOne<Domain.Identity.Module>().WithMany().HasForeignKey(m => m.ModuleId);
}

internal sealed class CodeSequenceConfiguration : IEntityTypeConfiguration<CodeSequence>
{
    public void Configure(EntityTypeBuilder<CodeSequence> builder)
    {
        builder.HasKey(c => c.Prefix);
        builder.Property(c => c.Prefix).HasMaxLength(50);
        builder.Property(c => c.RowVersion).IsRowVersion();
    }
}

internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.Property(a => a.EntityName).HasMaxLength(100);
        builder.Property(a => a.EntityId).HasMaxLength(64);
        builder.Property(a => a.Action).HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.ChangedAtUtc).HasColumnType("datetime2(3)");
        builder.HasOne<Domain.Identity.User>().WithMany().HasForeignKey(a => a.ChangedByUserId);
        builder.HasIndex(a => new { a.EntityName, a.EntityId });
        builder.HasIndex(a => a.ChangedAtUtc);
    }
}

internal sealed class LabReportConfiguration : IEntityTypeConfiguration<LabReport>
{
    public void Configure(EntityTypeBuilder<LabReport> builder)
    {
        builder.Property(r => r.ReportType).HasConversion<string>().HasMaxLength(40);
        builder.Property(r => r.PatientCode).HasMaxLength(200);
        builder.Property(r => r.PatientName).HasMaxLength(200);
        builder.Property(r => r.Age).HasMaxLength(50);
        builder.Property(r => r.Sex).HasMaxLength(20);
        builder.Property(r => r.CompanyOrPhysician).HasMaxLength(200);
        builder.Property(r => r.Remarks).HasMaxLength(500);
        builder.Property(r => r.MedicalTechnologist).HasMaxLength(100);
        builder.Property(r => r.Pathologist).HasMaxLength(100);
        builder.HasOne(r => r.Patient).WithMany().HasForeignKey(r => r.PatientId);
        builder.HasOne(r => r.PatientRegistration).WithMany().HasForeignKey(r => r.PatientRegistrationId);
        builder.HasOne(r => r.Photo).WithOne().HasForeignKey<LabReportPhoto>(p => p.LabReportId);
        builder.HasIndex(r => new { r.ReportType, r.DateRequested });
    }
}

internal sealed class LabReportPhotoConfiguration : IEntityTypeConfiguration<LabReportPhoto>
{
    public void Configure(EntityTypeBuilder<LabReportPhoto> builder)
    {
        builder.HasKey(p => p.LabReportId);
        builder.Property(p => p.ContentType).HasMaxLength(100);
    }
}
