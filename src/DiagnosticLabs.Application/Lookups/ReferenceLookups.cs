using DiagnosticLabs.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Application.Lookups;

public sealed record LookupOption(long Id, string Name);

public sealed record ServiceOption(long Id, string Name, decimal Price);

/// <summary>Active rows of the reference lists, for dropdowns on other screens.</summary>
public interface IReferenceLookups
{
    Task<IReadOnlyList<LookupOption>> GetCompaniesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ServiceOption>> GetServicesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LookupOption>> GetItemLocationsAsync(CancellationToken cancellationToken = default);
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
            .OrderBy(s => s.ServiceName)
            .Select(s => new ServiceOption(s.Id, s.ServiceName, s.Price))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<LookupOption>> GetItemLocationsAsync(CancellationToken cancellationToken = default) =>
        await db.ItemLocations.AsNoTracking().Where(l => l.IsActive)
            .OrderBy(l => l.ItemLocationName)
            .Select(l => new LookupOption(l.Id, l.ItemLocationName))
            .ToListAsync(cancellationToken);
}
