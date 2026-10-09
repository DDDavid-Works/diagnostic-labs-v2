using DiagnosticLabs.Domain.Common;
using DiagnosticLabs.Domain.Inventory;
using DiagnosticLabs.Domain.Patients;

namespace DiagnosticLabs.Domain.Catalog;

public class Service : ReferenceEntity
{
    public string ServiceName { get; set; } = string.Empty;

    public string ServiceDescription { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public ICollection<ServiceItemQuantity> Items { get; set; } = [];
}

public class Package : ReferenceEntity
{
    public string PackageName { get; set; } = string.Empty;

    public string PackageDescription { get; set; } = string.Empty;

    public decimal Price { get; set; }

    /// <summary>Set when the package is specific to one company.</summary>
    public long? CompanyId { get; set; }

    public Company? Company { get; set; }

    public ICollection<PackageService> Services { get; set; } = [];
}

public class PackageService : SoftDeletableEntity
{
    public long PackageId { get; set; }

    public Package Package { get; set; } = null!;

    public long ServiceId { get; set; }

    public Service Service { get; set; } = null!;

    public decimal Price { get; set; }
}

public class Discount : ReferenceEntity
{
    public string DiscountName { get; set; } = string.Empty;

    public string DiscountDescription { get; set; } = string.Empty;

    public ICollection<DiscountDetail> Details { get; set; } = [];
}

/// <summary>Exactly one of <see cref="Amount"/> or <see cref="Percentage"/> is set.</summary>
public class DiscountDetail : SoftDeletableEntity
{
    public long DiscountId { get; set; }

    public Discount Discount { get; set; } = null!;

    public decimal? Amount { get; set; }

    public decimal? Percentage { get; set; }
}

public class Department : ReferenceEntity
{
    public string DepartmentName { get; set; } = string.Empty;

    public string DepartmentDescription { get; set; } = string.Empty;
}
