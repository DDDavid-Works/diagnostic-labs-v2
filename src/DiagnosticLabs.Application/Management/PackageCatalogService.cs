using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Domain.Catalog;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Application.Management;

/// <summary>A service included in a package, at the price it has inside the package.</summary>
public sealed record PackageServiceLine(long Id, long ServiceId, decimal Price);

public sealed record PackageListItem(long Id, string PackageName, string PackageDescription, decimal Price, string? CompanyName, bool IsActive) : IHasId;

public sealed record PackageDetails(
    long Id,
    string PackageName,
    string PackageDescription,
    decimal Price,
    long? CompanyId,
    bool IsActive,
    IReadOnlyList<PackageServiceLine> Services,
    byte[] RowVersion) : IHasId;

public sealed record PackageInput(
    long Id,
    string? PackageName,
    string? PackageDescription,
    decimal Price,
    long? CompanyId,
    bool IsActive,
    IReadOnlyList<PackageServiceLine> Services,
    byte[]? RowVersion) : IReferenceInput;

public interface IPackageCatalogService : ICrudService<PackageListItem, PackageDetails, PackageInput>;

public sealed class PackageCatalogService(IAppDbContext db, ICurrentUser user)
    : ReferenceCrudService<Package, PackageListItem, PackageDetails, PackageInput>(db, user, ModuleIds.Packages, "Package"),
      IPackageCatalogService
{
    protected override DbSet<Package> Set => Db.Packages;

    protected override IQueryable<Package> ForListing(IQueryable<Package> q) => q.Include(p => p.Company);

    protected override IQueryable<Package> ForEditing(IQueryable<Package> q) => q.Include(p => p.Services);

    protected override IQueryable<Package> Matches(IQueryable<Package> q, string word) =>
        q.Where(p => p.PackageName.Contains(word) || p.PackageDescription.Contains(word));

    protected override IOrderedQueryable<Package> Order(IQueryable<Package> q) => q.OrderBy(p => p.PackageName).ThenBy(p => p.Id);

    protected override PackageListItem ToListItem(Package p) =>
        new(p.Id, p.PackageName, p.PackageDescription, p.Price, p.Company?.CompanyName, p.IsActive);

    protected override PackageDetails ToDetails(Package p) =>
        new(p.Id, p.PackageName, p.PackageDescription, p.Price, p.CompanyId, p.IsActive,
            [.. p.Services.Where(s => !s.IsDeleted).OrderBy(s => s.Id).Select(s => new PackageServiceLine(s.Id, s.ServiceId, s.Price))],
            p.RowVersion);

    protected override IReadOnlyList<string> Validate(PackageInput input)
    {
        var errors = new List<string>();
        Rules.Required(errors, input.PackageName, "Name", 50);
        Rules.Required(errors, input.PackageDescription, "Description", 200);
        Rules.NotNegative(errors, input.Price, "Package price");

        if (input.Services.Any(s => s.Price < 0))
            errors.Add("Service prices must not be negative.");
        if (input.Services.GroupBy(s => s.ServiceId).Any(g => g.Count() > 1))
            errors.Add("A service can only be included once in a package.");

        return errors;
    }

    protected override async Task<IReadOnlyList<string>> ValidateAsync(PackageInput input, CancellationToken cancellationToken)
    {
        var errors = new List<string>();

        var serviceIds = input.Services.Select(s => s.ServiceId).Distinct().ToList();
        if (await Db.Services.CountAsync(s => serviceIds.Contains(s.Id), cancellationToken) != serviceIds.Count)
            errors.Add("One or more included services no longer exist.");

        if (input.CompanyId is { } companyId && !await Db.Companies.AnyAsync(c => c.Id == companyId, cancellationToken))
            errors.Add("The selected company no longer exists.");

        return errors;
    }

    protected override void ApplyFields(Package package, PackageInput input)
    {
        package.PackageName = input.PackageName!.Trim();
        package.PackageDescription = input.PackageDescription!.Trim();
        package.Price = input.Price;
        package.CompanyId = input.CompanyId;

        SyncChildren(
            package.Services,
            input.Services,
            line => line.Id,
            child => child.Id,
            (child, line) => child.ServiceId == line.ServiceId,
            (child, line) =>
            {
                child.ServiceId = line.ServiceId;
                child.Price = line.Price;
            },
            child => Db.PackageServices.Remove(child));
    }
}
