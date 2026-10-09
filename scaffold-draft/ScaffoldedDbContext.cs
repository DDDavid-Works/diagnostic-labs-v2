using System;
using System.Collections.Generic;
using DiagnosticLabs.Infrastructure.Persistence.Scaffolded.Entities;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Infrastructure.Persistence.Scaffolded;

public partial class ScaffoldedDbContext : DbContext
{
    public ScaffoldedDbContext(DbContextOptions<ScaffoldedDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Ape> Apes { get; set; }

    public virtual DbSet<ClinicalChemistries1> ClinicalChemistries1s { get; set; }

    public virtual DbSet<ClinicalChemistries2> ClinicalChemistries2s { get; set; }

    public virtual DbSet<ClinicalChemistry> ClinicalChemistries { get; set; }

    public virtual DbSet<Company> Companies { get; set; }

    public virtual DbSet<CompanySetup> CompanySetups { get; set; }

    public virtual DbSet<DefaultValue> DefaultValues { get; set; }

    public virtual DbSet<Department> Departments { get; set; }

    public virtual DbSet<Discount> Discounts { get; set; }

    public virtual DbSet<DiscountDetail> DiscountDetails { get; set; }

    public virtual DbSet<Hematology> Hematologies { get; set; }

    public virtual DbSet<Immunology> Immunologies { get; set; }

    public virtual DbSet<Item> Items { get; set; }

    public virtual DbSet<ItemLocation> ItemLocations { get; set; }

    public virtual DbSet<ItemQuantity> ItemQuantities { get; set; }

    public virtual DbSet<LabResult> LabResults { get; set; }

    public virtual DbSet<LabResultsDefault> LabResultsDefaults { get; set; }

    public virtual DbSet<LatestCodeNumber> LatestCodeNumbers { get; set; }

    public virtual DbSet<Mer> Mers { get; set; }

    public virtual DbSet<Module> Modules { get; set; }

    public virtual DbSet<ModuleType> ModuleTypes { get; set; }

    public virtual DbSet<MultiLineEntry> MultiLineEntries { get; set; }

    public virtual DbSet<Package> Packages { get; set; }

    public virtual DbSet<PackageService> PackageServices { get; set; }

    public virtual DbSet<Patient> Patients { get; set; }

    public virtual DbSet<PatientCompany> PatientCompanies { get; set; }

    public virtual DbSet<PatientRegistration> PatientRegistrations { get; set; }

    public virtual DbSet<PatientRegistrationBatch> PatientRegistrationBatches { get; set; }

    public virtual DbSet<PatientRegistrationDetail> PatientRegistrationDetails { get; set; }

    public virtual DbSet<PatientRegistrationPayment> PatientRegistrationPayments { get; set; }

    public virtual DbSet<PatientRegistrationService> PatientRegistrationServices { get; set; }

    public virtual DbSet<Payment> Payments { get; set; }

    public virtual DbSet<PaymentDetail> PaymentDetails { get; set; }

    public virtual DbSet<PregnancyTest> PregnancyTests { get; set; }

    public virtual DbSet<Serology> Serologies { get; set; }

    public virtual DbSet<Service> Services { get; set; }

    public virtual DbSet<ServiceItemQuantity> ServiceItemQuantities { get; set; }

    public virtual DbSet<SingleLineEntry> SingleLineEntries { get; set; }

    public virtual DbSet<StoolFecalyse> StoolFecalyses { get; set; }

    public virtual DbSet<Urinalyse> Urinalyses { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserPermission> UserPermissions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Ape>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("APE_Id");

            entity.Property(e => e.AbdomenLiverSpleen).IsFixedLength();
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Ears).IsFixedLength();
            entity.Property(e => e.ExtremetiesSpine).IsFixedLength();
            entity.Property(e => e.Eyes).IsFixedLength();
            entity.Property(e => e.HeadScalp).IsFixedLength();
            entity.Property(e => e.HeartLungs).IsFixedLength();
            entity.Property(e => e.HeightWeightBy).HasDefaultValue("");
            entity.Property(e => e.InguinalAreaGenitalsAnus).IsFixedLength();
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Lmptype).HasDefaultValue("");
            entity.Property(e => e.MassCyst).IsFixedLength();
            entity.Property(e => e.NeckLymphNodesThyroid).IsFixedLength();
            entity.Property(e => e.Nose).IsFixedLength();
            entity.Property(e => e.OthersPe).IsFixedLength();
            entity.Property(e => e.Skin).IsFixedLength();
            entity.Property(e => e.Tattoo).IsFixedLength();
            entity.Property(e => e.TeethTonsilsThroatPharynx).IsFixedLength();
            entity.Property(e => e.ThoraxBreast).IsFixedLength();
            entity.Property(e => e.UpdatedDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.VitalSignsBy).HasDefaultValue("");
        });

        modelBuilder.Entity<ClinicalChemistries1>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("ClinicalChemistry1_Id");

            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.UpdatedDate).HasDefaultValueSql("(getdate())");
        });

        modelBuilder.Entity<ClinicalChemistries2>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("ClinicalChemistry2_Id");

            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.UpdatedDate).HasDefaultValueSql("(getdate())");
        });

        modelBuilder.Entity<ClinicalChemistry>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("ClinicalChemistry_Id");

            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.UpdatedDate).HasDefaultValueSql("(getdate())");
        });

        modelBuilder.Entity<Company>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Company_Id");

            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.UpdatedDate).HasDefaultValueSql("(getdate())");
        });

        modelBuilder.Entity<CompanySetup>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("CompanySetup_Id");

            entity.Property(e => e.UpdatedDate).HasDefaultValueSql("(getdate())");
        });

        modelBuilder.Entity<DefaultValue>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("DefaultValue_Id");

            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.UpdatedDate).HasDefaultValueSql("(getdate())");
        });

        modelBuilder.Entity<Department>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Department_Id");

            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.UpdatedDate).HasDefaultValueSql("(getdate())");
        });

        modelBuilder.Entity<Discount>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Discount_Id");

            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.UpdatedDate).HasDefaultValueSql("(getdate())");
        });

        modelBuilder.Entity<DiscountDetail>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("DiscountDetail_Id");

            entity.Property(e => e.Amount).HasDefaultValueSql("(NULL)");
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Percentage).HasDefaultValueSql("(NULL)");
            entity.Property(e => e.UpdatedDate).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.Discount).WithMany(p => p.DiscountDetails)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DiscountDetail_Discounts");
        });

        modelBuilder.Entity<Hematology>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Hematology_Id");

            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.UpdatedDate).HasDefaultValueSql("(getdate())");
        });

        modelBuilder.Entity<Immunology>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Immunology_Id");

            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.UpdatedDate).HasDefaultValueSql("(getdate())");
        });

        modelBuilder.Entity<Item>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Item_Id");

            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.UpdatedDate).HasDefaultValueSql("(getdate())");
        });

        modelBuilder.Entity<ItemLocation>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("ItemLocation_Id");

            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.UpdatedDate).HasDefaultValueSql("(getdate())");
        });

        modelBuilder.Entity<ItemQuantity>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("ItemQuantity_Id");

            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.UpdatedDate).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.Item).WithMany(p => p.ItemQuantities)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ItemQuantity_Items");

            entity.HasOne(d => d.ItemLocation).WithMany(p => p.ItemQuantities)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ItemQuantity_ItemLocations");
        });

        modelBuilder.Entity<LabResult>(entity =>
        {
            entity.ToView("LabResults");
        });

        modelBuilder.Entity<LabResultsDefault>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("LabResultsDefault_Id");

            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.UpdatedDate).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.Module).WithMany(p => p.LabResultsDefaults).HasConstraintName("FK_LabResultsDefault_Modules");
        });

        modelBuilder.Entity<LatestCodeNumber>(entity =>
        {
            entity.ToView("LatestCodeNumbers");
        });

        modelBuilder.Entity<Mer>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("MER_Id");

            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.UpdatedDate).HasDefaultValueSql("(getdate())");
        });

        modelBuilder.Entity<Module>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Module_Id");

            entity.Property(e => e.Icon).HasDefaultValue("");
            entity.Property(e => e.IsActive).HasDefaultValue(true);

            entity.HasOne(d => d.ModuleType).WithMany(p => p.Modules)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Modules_ModuleTypes");

            entity.HasOne(d => d.Service).WithMany(p => p.Modules).HasConstraintName("FK_Modules_Services");
        });

        modelBuilder.Entity<ModuleType>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("ModuleType_Id");

            entity.Property(e => e.Icon).HasDefaultValue("");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.IsAdmin).HasDefaultValue(true);
        });

        modelBuilder.Entity<MultiLineEntry>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("MultiLineEntry_Id");

            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.UpdatedDate).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.Module).WithMany(p => p.MultiLineEntries).HasConstraintName("FK_MultiLineEntry_Modules");
        });

        modelBuilder.Entity<Package>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Package_Id");

            entity.Property(e => e.CompanyId).HasDefaultValue(0L);
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.UpdatedDate).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.Company).WithMany(p => p.Packages).HasConstraintName("FK_Package_Companies");
        });

        modelBuilder.Entity<PackageService>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PackageService_Id");

            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.UpdatedDate).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.Package).WithMany(p => p.PackageServices)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PackageService_Packages");

            entity.HasOne(d => d.Service).WithMany(p => p.PackageServices)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PackageService_Services");
        });

        modelBuilder.Entity<Patient>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Patient_Id");

            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.PatientName).HasDefaultValue("");
            entity.Property(e => e.UpdatedDate).HasDefaultValueSql("(getdate())");
        });

        modelBuilder.Entity<PatientCompany>(entity =>
        {
            entity.ToView("PatientCompanies");
        });

        modelBuilder.Entity<PatientRegistration>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PatientRegistration_Id");

            entity.Property(e => e.CompanyId).HasDefaultValue(0L);
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.DiscountAmount).HasDefaultValue(0m);
            entity.Property(e => e.DiscountPercentage).HasDefaultValueSql("(NULL)");
            entity.Property(e => e.InputDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.PackageId).HasDefaultValue(0L);
            entity.Property(e => e.RegistrationCode).HasDefaultValue("");
            entity.Property(e => e.UpdatedDate).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.Company).WithMany(p => p.PatientRegistrations).HasConstraintName("FK_PatientRegistration_Companies");

            entity.HasOne(d => d.Package).WithMany(p => p.PatientRegistrations).HasConstraintName("FK_PatientRegistration_Packages");

            entity.HasOne(d => d.Patient).WithMany(p => p.PatientRegistrations)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PatientRegistration_Patients");
        });

        modelBuilder.Entity<PatientRegistrationBatch>(entity =>
        {
            entity.ToView("PatientRegistrationBatches");
        });

        modelBuilder.Entity<PatientRegistrationDetail>(entity =>
        {
            entity.ToView("PatientRegistrationDetails");
        });

        modelBuilder.Entity<PatientRegistrationPayment>(entity =>
        {
            entity.ToView("PatientRegistrationPayments");
        });

        modelBuilder.Entity<PatientRegistrationService>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PatientRegistrationService_Id");

            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.UpdatedDate).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.PatientRegistration).WithMany(p => p.PatientRegistrationServices)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PatientRegistrationService_Patients");

            entity.HasOne(d => d.Service).WithMany(p => p.PatientRegistrationServices)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PatientRegistrationService_Services");
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PaymentId_Id");

            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.PaymentDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.UpdatedDate).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.PatientRegistration).WithMany(p => p.Payments)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Payment_PatientRegistration");
        });

        modelBuilder.Entity<PaymentDetail>(entity =>
        {
            entity.ToView("PaymentDetails");
        });

        modelBuilder.Entity<PregnancyTest>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PregnancyTest_Id");

            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.UpdatedDate).HasDefaultValueSql("(getdate())");
        });

        modelBuilder.Entity<Serology>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Serology_Id");

            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.UpdatedDate).HasDefaultValueSql("(getdate())");
        });

        modelBuilder.Entity<Service>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Service_Id");

            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.UpdatedDate).HasDefaultValueSql("(getdate())");
        });

        modelBuilder.Entity<ServiceItemQuantity>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("ServiceItemLocationQuantity_Id");

            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.UpdatedDate).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.Item).WithMany(p => p.ServiceItemQuantities)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ServiceItemLocationQuantity_Items");

            entity.HasOne(d => d.Service).WithMany(p => p.ServiceItemQuantities)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ServiceItemLocationQuantity_Services");
        });

        modelBuilder.Entity<SingleLineEntry>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("SingleLineEntry_Id");

            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.UpdatedDate).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.Module).WithMany(p => p.SingleLineEntries).HasConstraintName("FK_SingleLineEntry_Modules");
        });

        modelBuilder.Entity<StoolFecalyse>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("StoolFecalysis_Id");

            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.UpdatedDate).HasDefaultValueSql("(getdate())");
        });

        modelBuilder.Entity<Urinalyse>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Urinalysis_Id");

            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.UpdatedDate).HasDefaultValueSql("(getdate())");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("User_Id");

            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.UpdatedDate).HasDefaultValueSql("(getdate())");
        });

        modelBuilder.Entity<UserPermission>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("UserPermission_Id");

            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.UpdatedDate).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.Module).WithMany(p => p.UserPermissions)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserPermissions_Modules");

            entity.HasOne(d => d.User).WithMany(p => p.UserPermissions)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserPermissions_Users");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
