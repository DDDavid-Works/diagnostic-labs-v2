using CommunityToolkit.Mvvm.ComponentModel;
using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Management;
using DiagnosticLabs.Wpf.Services;
using Microsoft.Extensions.Logging;

namespace DiagnosticLabs.Wpf.ViewModels;

public partial class CompaniesViewModel(
    IServiceRunner runner, IDialogService dialogs, ICurrentUser user, ILogger<CompaniesViewModel> logger)
    : CrudViewModel<ICompanyService, CompanyListItem, CompanyDetails, CompanyInput>(
        runner, dialogs, user, ModuleIds.Companies, "Companies", "Company", hasActiveFlag: true, logger)
{
    [ObservableProperty]
    private string _companyName = string.Empty;

    [ObservableProperty]
    private string _address = string.Empty;

    [ObservableProperty]
    private string _contactNumbers = string.Empty;

    [ObservableProperty]
    private string _contactPerson = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave), nameof(CanDelete))]
    private bool _isSystem;

    protected override bool IsLocked => IsSystem;

    protected override string CurrentName => CompanyName;

    protected override CompanyInput BuildInput() => new(Id, CompanyName, Address, ContactNumbers, ContactPerson, IsActive, RowVersion);

    protected override void ShowFields(CompanyDetails d)
    {
        CompanyName = d.CompanyName;
        Address = d.Address;
        ContactNumbers = d.ContactNumbers;
        ContactPerson = d.ContactPerson;
        IsActive = d.IsActive;
        IsSystem = d.IsSystem;
        RowVersion = d.RowVersion;
    }

    protected override void ResetFields()
    {
        CompanyName = Address = ContactNumbers = ContactPerson = string.Empty;
        IsSystem = false;
    }
}

public partial class DepartmentsViewModel(
    IServiceRunner runner, IDialogService dialogs, ICurrentUser user, ILogger<DepartmentsViewModel> logger)
    : CrudViewModel<IDepartmentService, DepartmentListItem, DepartmentDetails, DepartmentInput>(
        runner, dialogs, user, ModuleIds.Departments, "Departments", "Department", hasActiveFlag: true, logger)
{
    [ObservableProperty]
    private string _departmentName = string.Empty;

    [ObservableProperty]
    private string _departmentDescription = string.Empty;

    protected override string CurrentName => DepartmentName;

    protected override DepartmentInput BuildInput() => new(Id, DepartmentName, DepartmentDescription, IsActive, RowVersion);

    protected override void ShowFields(DepartmentDetails d)
    {
        DepartmentName = d.DepartmentName;
        DepartmentDescription = d.DepartmentDescription;
        IsActive = d.IsActive;
        RowVersion = d.RowVersion;
    }

    protected override void ResetFields() => DepartmentName = DepartmentDescription = string.Empty;
}

public partial class ItemLocationsViewModel(
    IServiceRunner runner, IDialogService dialogs, ICurrentUser user, ILogger<ItemLocationsViewModel> logger)
    : CrudViewModel<IItemLocationService, ItemLocationListItem, ItemLocationDetails, ItemLocationInput>(
        runner, dialogs, user, ModuleIds.ItemLocations, "Item locations", "Item location", hasActiveFlag: true, logger)
{
    [ObservableProperty]
    private string _itemLocationName = string.Empty;

    protected override string CurrentName => ItemLocationName;

    protected override ItemLocationInput BuildInput() => new(Id, ItemLocationName, IsActive, RowVersion);

    protected override void ShowFields(ItemLocationDetails d)
    {
        ItemLocationName = d.ItemLocationName;
        IsActive = d.IsActive;
        RowVersion = d.RowVersion;
    }

    protected override void ResetFields() => ItemLocationName = string.Empty;
}

public partial class ServicesViewModel(
    IServiceRunner runner, IDialogService dialogs, ICurrentUser user, ILogger<ServicesViewModel> logger)
    : CrudViewModel<IServiceCatalogService, ServiceListItem, ServiceDetails, ServiceInput>(
        runner, dialogs, user, ModuleIds.Services, "Services", "Service", hasActiveFlag: true, logger)
{
    [ObservableProperty]
    private string _serviceName = string.Empty;

    [ObservableProperty]
    private string _serviceDescription = string.Empty;

    [ObservableProperty]
    private decimal _price;

    protected override string CurrentName => ServiceName;

    protected override ServiceInput BuildInput() => new(Id, ServiceName, ServiceDescription, Price, IsActive, RowVersion);

    protected override void ShowFields(ServiceDetails d)
    {
        ServiceName = d.ServiceName;
        ServiceDescription = d.ServiceDescription;
        Price = d.Price;
        IsActive = d.IsActive;
        RowVersion = d.RowVersion;
    }

    protected override void ResetFields()
    {
        ServiceName = ServiceDescription = string.Empty;
        Price = 0;
    }
}
