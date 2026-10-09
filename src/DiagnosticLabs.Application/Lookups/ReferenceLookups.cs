using DiagnosticLabs.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Application.Lookups;

public sealed record LookupOption(long Id, string Name);

public sealed record ServiceOption(long Id, string Name, decimal Price);

public sealed record PackageOption(long Id, string Name, decimal Price);

/// <summary>One way a discount can be given: a fixed amount or a percentage.</summary>
public sealed record DiscountChoice(decimal? Amount, decimal? Percentage)
{
    public string Label => Percentage is { } p ? $"{p:0.##}%" : $"{Amount:N2}";
}

/// <summary>A maintained discount (e.g. Senior Citizen) and the ways it can be given.</summary>
public sealed record DiscountOption(long Id, string Name, IReadOnlyList<DiscountChoice> Choices);

/// <summary>Active rows of the reference lists, for dropdowns on other screens.</summary>
public interface IReferenceLookups
{
    Task<IReadOnlyList<LookupOption>> GetCompaniesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ServiceOption>> GetServicesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LookupOption>> GetItemLocationsAsync(CancellationToken cancellationToken = default);

    /// <summary>The packages offered to a company; <c>null</c> means no company, which sees the general packages (those not tied to a company).</summary>
    Task<IReadOnlyList<PackageOption>> GetPackagesAsync(long? companyId, CancellationToken cancellationToken = default);

    /// <summary>The services of a package at their package prices (<see cref="ServiceOption.Id"/> is the service id).</summary>
    Task<IReadOnlyList<ServiceOption>> GetPackageServicesAsync(long packageId, CancellationToken cancellationToken = default);

    /// <summary>Batch names already used by a company (or by registrations without one), for the batch dropdown.</summary>
    Task<IReadOnlyList<string>> GetBatchesAsync(long? companyId, CancellationToken cancellationToken = default);

    /// <summary>Active discounts with their options; <paramref name="alsoId"/> keeps one that was switched off since it was used.</summary>
    Task<IReadOnlyList<DiscountOption>> GetDiscountsAsync(long? alsoId = null, CancellationToken cancellationToken = default);
}

public sealed class ReferenceLookups(IAppDbContext db) : IReferenceLookups
{
    public async Task<IReadOnlyList<LookupOption>> GetCompaniesAsync(CancellationToken cancellationToken = default) =>
        await db.Companies.AsNoTracking().Where(c => c.IsActive)
            .OrderBy(c => c.CompanyName)
            .Select(c => new LookupOption(c.Id, c.CompanyName))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ServiceOption>> GetServicesAsync(CancellationToken cancellationToken = default) =>
        await db.Services.AsNoTracking().Where(s => s.IsActive)
            .OrderBy(s => s.Id) // the order the services were created in, which is the order the lab uses
            .Select(s => new ServiceOption(s.Id, s.ServiceName, s.Price))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<LookupOption>> GetItemLocationsAsync(CancellationToken cancellationToken = default) =>
        await db.ItemLocations.AsNoTracking().Where(l => l.IsActive)
            .OrderBy(l => l.ItemLocationName)
            .Select(l => new LookupOption(l.Id, l.ItemLocationName))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<PackageOption>> GetPackagesAsync(long? companyId, CancellationToken cancellationToken = default) =>
        await db.Packages.AsNoTracking().Where(p => p.IsActive && p.CompanyId == companyId)
            .OrderBy(p => p.PackageName).ThenBy(p => p.Id)
            .Select(p => new PackageOption(p.Id, p.PackageName, p.Price))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ServiceOption>> GetPackageServicesAsync(long packageId, CancellationToken cancellationToken = default) =>
        await db.PackageServices.AsNoTracking().Where(s => s.PackageId == packageId)
            .OrderBy(s => s.Id)
            .Select(s => new ServiceOption(s.ServiceId, s.Service.ServiceName, s.Price))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<string>> GetBatchesAsync(long? companyId, CancellationToken cancellationToken = default) =>
        await db.PatientRegistrations.AsNoTracking()
            .Where(r => r.CompanyId == companyId && r.BatchName != string.Empty)
            .Select(r => r.BatchName)
            .Distinct()
            .OrderBy(b => b)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<DiscountOption>> GetDiscountsAsync(long? alsoId = null, CancellationToken cancellationToken = default)
    {
        var rows = await db.Discounts.AsNoTracking().Include(d => d.Details)
            .Where(d => d.IsActive || d.Id == alsoId)
            .OrderBy(d => d.DiscountName).ThenBy(d => d.Id)
            .ToListAsync(cancellationToken);

        return
        [
            .. rows.Select(d => new DiscountOption(
                d.Id,
                d.DiscountName,
                [.. d.Details.Where(x => !x.IsDeleted).OrderBy(x => x.Id).Select(x => new DiscountChoice(x.Amount, x.Percentage))])),
        ];
    }
}