using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Domain.Catalog;
using DiagnosticLabs.Domain.Inventory;
using DiagnosticLabs.Domain.Patients;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Application.Management;

internal static class Rules
{
    public static void Required(List<string> errors, string? value, string label, int maxLength)
    {
        var text = value?.Trim() ?? string.Empty;
        if (text.Length == 0)
            errors.Add($"{label} can not be empty.");
        else if (text.Length > maxLength)
            errors.Add($"{label} can not be longer than {maxLength} characters.");
    }

    public static void MaxLength(List<string> errors, string? value, string label, int maxLength)
    {
        if ((value?.Trim().Length ?? 0) > maxLength)
            errors.Add($"{label} can not be longer than {maxLength} characters.");
    }

    public static void NotNegative(List<string> errors, decimal value, string label)
    {
        if (value < 0)
            errors.Add($"{label} must not be negative.");
    }
}

// ---------------------------------------------------------------- Companies

public sealed record CompanyListItem(long Id, string CompanyName, string ContactPerson, string ContactNumbers, bool IsActive, bool IsSystem) : IHasId;

public sealed record CompanyDetails(
    long Id, string CompanyName, string Address, string ContactNumbers, string ContactPerson, bool IsActive, bool IsSystem, byte[] RowVersion) : IHasId;

public sealed record CompanyInput(
    long Id, string? CompanyName, string? Address, string? ContactNumbers, string? ContactPerson, bool IsActive, byte[]? RowVersion) : IReferenceInput;

public interface ICompanyService : ICrudService<CompanyListItem, CompanyDetails, CompanyInput>;

public sealed class CompanyService(IAppDbContext db, ICurrentUser user)
    : ReferenceCrudService<Company, CompanyListItem, CompanyDetails, CompanyInput>(db, user, ModuleIds.Companies, "Company"),
      ICompanyService
{
    protected override DbSet<Company> Set => Db.Companies;

    protected override IQueryable<Company> Matches(IQueryable<Company> q, string word) =>
        q.Where(c => c.CompanyName.Contains(word) || c.ContactPerson.Contains(word));

    protected override IOrderedQueryable<Company> Order(IQueryable<Company> q) => q.OrderBy(c => c.CompanyName).ThenBy(c => c.Id);

    protected override CompanyListItem ToListItem(Company c) => new(c.Id, c.CompanyName, c.ContactPerson, c.ContactNumbers, c.IsActive, c.IsSystem);

    protected override CompanyDetails ToDetails(Company c) =>
        new(c.Id, c.CompanyName, c.Address, c.ContactNumbers, c.ContactPerson, c.IsActive, c.IsSystem, c.RowVersion);

    protected override IReadOnlyList<string> Validate(CompanyInput input)
    {
        var errors = new List<string>();
        Rules.Required(errors, input.CompanyName, "Name", 200);
        Rules.MaxLength(errors, input.Address, "Address", 500);
        Rules.MaxLength(errors, input.ContactNumbers, "Contact numbers", 200);
        Rules.MaxLength(errors, input.ContactPerson, "Contact person", 200);
        return errors;
    }

    protected override void ApplyFields(Company c, CompanyInput input)
    {
        c.CompanyName = input.CompanyName!.Trim();
        c.Address = input.Address?.Trim() ?? string.Empty;
        c.ContactNumbers = input.ContactNumbers?.Trim() ?? string.Empty;
        c.ContactPerson = input.ContactPerson?.Trim() ?? string.Empty;
    }

    protected override Error? CheckCanModify(Company company) =>
        company.IsSystem ? new Error("Company.System", "This is a system record and can not be changed or removed.") : null;
}

// ---------------------------------------------------------------- Departments

public sealed record DepartmentListItem(long Id, string DepartmentName, string DepartmentDescription, bool IsActive) : IHasId;

public sealed record DepartmentDetails(long Id, string DepartmentName, string DepartmentDescription, bool IsActive, byte[] RowVersion) : IHasId;

public sealed record DepartmentInput(long Id, string? DepartmentName, string? DepartmentDescription, bool IsActive, byte[]? RowVersion) : IReferenceInput;

public interface IDepartmentService : ICrudService<DepartmentListItem, DepartmentDetails, DepartmentInput>;

public sealed class DepartmentService(IAppDbContext db, ICurrentUser user)
    : ReferenceCrudService<Department, DepartmentListItem, DepartmentDetails, DepartmentInput>(db, user, ModuleIds.Departments, "Department"),
      IDepartmentService
{
    protected override DbSet<Department> Set => Db.Departments;

    protected override IQueryable<Department> Matches(IQueryable<Department> q, string word) =>
        q.Where(d => d.DepartmentName.Contains(word) || d.DepartmentDescription.Contains(word));

    protected override IOrderedQueryable<Department> Order(IQueryable<Department> q) => q.OrderBy(d => d.DepartmentName).ThenBy(d => d.Id);

    protected override DepartmentListItem ToListItem(Department d) => new(d.Id, d.DepartmentName, d.DepartmentDescription, d.IsActive);

    protected override DepartmentDetails ToDetails(Department d) => new(d.Id, d.DepartmentName, d.DepartmentDescription, d.IsActive, d.RowVersion);

    protected override IReadOnlyList<string> Validate(DepartmentInput input)
    {
        var errors = new List<string>();
        Rules.Required(errors, input.DepartmentName, "Name", 50);
        Rules.MaxLength(errors, input.DepartmentDescription, "Description", 200);
        return errors;
    }

    protected override void ApplyFields(Department d, DepartmentInput input)
    {
        d.DepartmentName = input.DepartmentName!.Trim();
        d.DepartmentDescription = input.DepartmentDescription?.Trim() ?? string.Empty;
    }
}

// ---------------------------------------------------------------- Item locations

public sealed record ItemLocationListItem(long Id, string ItemLocationName, bool IsActive) : IHasId;

public sealed record ItemLocationDetails(long Id, string ItemLocationName, bool IsActive, byte[] RowVersion) : IHasId;

public sealed record ItemLocationInput(long Id, string? ItemLocationName, bool IsActive, byte[]? RowVersion) : IReferenceInput;

public interface IItemLocationService : ICrudService<ItemLocationListItem, ItemLocationDetails, ItemLocationInput>;

public sealed class ItemLocationService(IAppDbContext db, ICurrentUser user)
    : ReferenceCrudService<ItemLocation, ItemLocationListItem, ItemLocationDetails, ItemLocationInput>(db, user, ModuleIds.ItemLocations, "Item location"),
      IItemLocationService
{
    protected override DbSet<ItemLocation> Set => Db.ItemLocations;

    protected override IQueryable<ItemLocation> Matches(IQueryable<ItemLocation> q, string word) =>
        q.Where(l => l.ItemLocationName.Contains(word));

    protected override IOrderedQueryable<ItemLocation> Order(IQueryable<ItemLocation> q) => q.OrderBy(l => l.ItemLocationName).ThenBy(l => l.Id);

    protected override ItemLocationListItem ToListItem(ItemLocation l) => new(l.Id, l.ItemLocationName, l.IsActive);

    protected override ItemLocationDetails ToDetails(ItemLocation l) => new(l.Id, l.ItemLocationName, l.IsActive, l.RowVersion);

    protected override IReadOnlyList<string> Validate(ItemLocationInput input)
    {
        var errors = new List<string>();
        Rules.Required(errors, input.ItemLocationName, "Name", 50);
        return errors;
    }

    protected override void ApplyFields(ItemLocation l, ItemLocationInput input) => l.ItemLocationName = input.ItemLocationName!.Trim();
}

// ---------------------------------------------------------------- Services

public sealed record ServiceListItem(long Id, string ServiceName, string ServiceDescription, decimal Price, bool IsActive) : IHasId;

public sealed record ServiceDetails(long Id, string ServiceName, string ServiceDescription, decimal Price, bool IsActive, byte[] RowVersion) : IHasId;

public sealed record ServiceInput(long Id, string? ServiceName, string? ServiceDescription, decimal Price, bool IsActive, byte[]? RowVersion) : IReferenceInput;

public interface IServiceCatalogService : ICrudService<ServiceListItem, ServiceDetails, ServiceInput>;

public sealed class ServiceCatalogService(IAppDbContext db, ICurrentUser user)
    : ReferenceCrudService<Service, ServiceListItem, ServiceDetails, ServiceInput>(db, user, ModuleIds.Services, "Service"),
      IServiceCatalogService
{
    protected override DbSet<Service> Set => Db.Services;

    protected override IQueryable<Service> Matches(IQueryable<Service> q, string word) =>
        q.Where(s => s.ServiceName.Contains(word) || s.ServiceDescription.Contains(word));

    protected override IOrderedQueryable<Service> Order(IQueryable<Service> q) => q.OrderBy(s => s.ServiceName).ThenBy(s => s.Id);

    protected override ServiceListItem ToListItem(Service s) => new(s.Id, s.ServiceName, s.ServiceDescription, s.Price, s.IsActive);

    protected override ServiceDetails ToDetails(Service s) => new(s.Id, s.ServiceName, s.ServiceDescription, s.Price, s.IsActive, s.RowVersion);

    protected override IReadOnlyList<string> Validate(ServiceInput input)
    {
        var errors = new List<string>();
        Rules.Required(errors, input.ServiceName, "Name", 50);
        Rules.Required(errors, input.ServiceDescription, "Description", 200);
        Rules.NotNegative(errors, input.Price, "Price");
        return errors;
    }

    protected override void ApplyFields(Service s, ServiceInput input)
    {
        s.ServiceName = input.ServiceName!.Trim();
        s.ServiceDescription = input.ServiceDescription!.Trim();
        s.Price = input.Price;
    }
}
