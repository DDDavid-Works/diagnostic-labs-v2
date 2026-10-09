using DiagnosticLabs.Domain.Catalog;
using DiagnosticLabs.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiagnosticLabs.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.Property(u => u.Username).HasMaxLength(100);
        builder.Property(u => u.FullName).HasMaxLength(200);
        builder.Property(u => u.PasswordHash).HasMaxLength(200);
        builder.HasIndex(u => u.Username).IsUnique();
        builder.HasMany(u => u.Permissions).WithOne().HasForeignKey(p => p.UserId);
    }
}

internal sealed class ModuleTypeConfiguration : IEntityTypeConfiguration<ModuleType>
{
    public void Configure(EntityTypeBuilder<ModuleType> builder)
    {
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.ModuleTypeName).HasMaxLength(50);
        builder.Property(m => m.Icon).HasMaxLength(100);
    }
}

internal sealed class ModuleConfiguration : IEntityTypeConfiguration<Module>
{
    public void Configure(EntityTypeBuilder<Module> builder)
    {
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.ModuleName).HasMaxLength(50);
        builder.Property(m => m.Icon).HasMaxLength(100);
        builder.HasOne(m => m.ModuleType).WithMany().HasForeignKey(m => m.ModuleTypeId);
        builder.HasOne<Service>().WithMany().HasForeignKey(m => m.ServiceId);
    }
}

internal sealed class UserPermissionConfiguration : IEntityTypeConfiguration<UserPermission>
{
    public void Configure(EntityTypeBuilder<UserPermission> builder)
    {
        builder.HasOne(p => p.Module).WithMany().HasForeignKey(p => p.ModuleId);
        builder.HasIndex(p => new { p.UserId, p.ModuleId }).IsUnique();
    }
}
