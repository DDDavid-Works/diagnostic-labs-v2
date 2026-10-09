using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Lookups;
using DiagnosticLabs.Application.Management;
using DiagnosticLabs.Wpf.Services;
using Microsoft.Extensions.Logging;

namespace DiagnosticLabs.Wpf.ViewModels;

// ------------------------------------------------------------------ row view models

public partial class ItemQuantityLineViewModel : ObservableObject
{
    public long Id { get; init; }

    [ObservableProperty]
    private long _itemLocationId;

    [ObservableProperty]
    private decimal _quantity;
}

public partial class DiscountLineViewModel : ObservableObject
{
    public long Id { get; init; }

    [ObservableProperty]
    private decimal? _amount;

    [ObservableProperty]
    private decimal? _percentage;
}

public partial class PackageServiceLineViewModel : ObservableObject
{
    public long Id { get; init; }

    public long ServiceId { get; init; }

    public string ServiceName { get; init; } = string.Empty;

    [ObservableProperty]
    private decimal _price;
}

// ------------------------------------------------------------------ items & stock

public partial class ItemsViewModel(
    IServiceRunner runner, IDialogService dialogs, ICurrentUser user, ILogger<ItemsViewModel> logger)
    : CrudViewModel<IItemService, ItemListItem, ItemDetails, ItemInput>(
        runner, dialogs, user, ModuleIds.Items, "Items", "Item", hasActiveFlag: true, logger)
{
    [ObservableProperty]
    private string _itemName = string.Empty;

    [ObservableProperty]
    private decimal _cost;

    [ObservableProperty]
    private ItemQuantityLineViewModel? _selectedQuantity;

    public ObservableCollection<ItemQuantityLineViewModel> Quantities { get; } = [];

    public ObservableCollection<LookupOption> Locations { get; } = [];

    public decimal TotalQuantity => Quantities.Sum(q => q.Quantity);

    protected override string CurrentName => ItemName;

    protected override async Task OnInitializeAsync()
    {
        var locations = await Call<IReferenceLookups, IReadOnlyList<LookupOption>>(s => s.GetItemLocationsAsync());
        Locations.Clear();
        foreach (var location in locations)
            Locations.Add(location);
    }

    protected override ItemInput BuildInput() => new(
        Id, ItemName, Cost, IsActive,
        [.. Quantities.Select(q => new ItemQuantityLine(q.Id, q.ItemLocationId, q.Quantity))],
        RowVersion);

    protected override void ShowFields(ItemDetails d)
    {
        ItemName = d.ItemName;
        Cost = d.Cost;
        IsActive = d.IsActive;
        RowVersion = d.RowVersion;
        SetQuantities(d.Quantities.Select(q => new ItemQuantityLineViewModel { Id = q.Id, ItemLocationId = q.ItemLocationId, Quantity = q.Quantity }));
    }

    protected override void ResetFields()
    {
        ItemName = string.Empty;
        Cost = 0;
        SetQuantities([]);
    }

    [RelayCommand]
    private void AddQuantity()
    {
        // Offer a location that is not already listed so the new row is valid straight away.
        var free = Locations.FirstOrDefault(l => Quantities.All(q => q.ItemLocationId != l.Id));
        var line = new ItemQuantityLineViewModel { ItemLocationId = free?.Id ?? 0 };
        Attach(line);
        Quantities.Add(line);
        SelectedQuantity = line;
        OnPropertyChanged(nameof(TotalQuantity));
    }

    [RelayCommand]
    private void RemoveQuantity()
    {
        if (SelectedQuantity is null)
            return;

        Detach(SelectedQuantity);
        Quantities.Remove(SelectedQuantity);
        OnPropertyChanged(nameof(TotalQuantity));
    }

    private void SetQuantities(IEnumerable<ItemQuantityLineViewModel> lines)
    {
        foreach (var existing in Quantities)
            Detach(existing);
        Quantities.Clear();

        foreach (var line in lines)
        {
            Attach(line);
            Quantities.Add(line);
        }

        OnPropertyChanged(nameof(TotalQuantity));
    }

    private void Attach(ItemQuantityLineViewModel line) => line.PropertyChanged += OnLineChanged;

    private void Detach(ItemQuantityLineViewModel line) => line.PropertyChanged -= OnLineChanged;

    private void OnLineChanged(object? sender, PropertyChangedEventArgs e) => OnPropertyChanged(nameof(TotalQuantity));
}

// ------------------------------------------------------------------ discounts

public partial class DiscountsViewModel(
    IServiceRunner runner, IDialogService dialogs, ICurrentUser user, ILogger<DiscountsViewModel> logger)
    : CrudViewModel<IDiscountService, DiscountListItem, DiscountDetails, DiscountInput>(
        runner, dialogs, user, ModuleIds.Discounts, "Discounts", "Discount", hasActiveFlag: true, logger)
{
    [ObservableProperty]
    private string _discountName = string.Empty;

    [ObservableProperty]
    private string _discountDescription = string.Empty;

    [ObservableProperty]
    private DiscountLineViewModel? _selectedLine;

    public ObservableCollection<DiscountLineViewModel> Lines { get; } = [];

    protected override string CurrentName => DiscountName;

    protected override DiscountInput BuildInput() => new(
        Id, DiscountName, DiscountDescription, IsActive,
        [.. Lines.Select(l => new DiscountLine(l.Id, l.Amount, l.Percentage))],
        RowVersion);

    protected override void ShowFields(DiscountDetails d)
    {
        DiscountName = d.DiscountName;
        DiscountDescription = d.DiscountDescription;
        IsActive = d.IsActive;
        RowVersion = d.RowVersion;
        Lines.Clear();
        foreach (var line in d.Lines)
            Lines.Add(new DiscountLineViewModel { Id = line.Id, Amount = line.Amount, Percentage = line.Percentage });
    }

    protected override void ResetFields()
    {
        DiscountName = DiscountDescription = string.Empty;
        Lines.Clear();
    }

    [RelayCommand]
    private void AddLine()
    {
        var line = new DiscountLineViewModel { Percentage = 0 };
        Lines.Add(line);
        SelectedLine = line;
    }

    [RelayCommand]
    private void RemoveLine()
    {
        if (SelectedLine is not null)
            Lines.Remove(SelectedLine);
    }
}

// ------------------------------------------------------------------ packages

public partial class PackagesViewModel(
    IServiceRunner runner,
    IDialogService dialogs,
    ICurrentUser user,
    IServicePickerDialog servicePicker,
    ILogger<PackagesViewModel> logger)
    : CrudViewModel<IPackageCatalogService, PackageListItem, PackageDetails, PackageInput>(
        runner, dialogs, user, ModuleIds.Packages, "Packages", "Package", hasActiveFlag: true, logger)
{
    private static readonly LookupOption AllCompanies = new(0, "(any company)");

    // The package price follows the sum of its services until the user types a price themselves.
    private bool _priceEdited;
    private bool _settingPrice;
    private bool _loading;

    [ObservableProperty]
    private string _packageName = string.Empty;

    [ObservableProperty]
    private string _packageDescription = string.Empty;

    [ObservableProperty]
    private decimal _price;

    [ObservableProperty]
    private long _companyChoiceId;

    public ObservableCollection<PackageServiceLineViewModel> Services { get; } = [];

    public ObservableCollection<LookupOption> Companies { get; } = [];

    public ObservableCollection<ServiceOption> ServiceOptions { get; } = [];

    protected override string CurrentName => PackageName;

    partial void OnPriceChanged(decimal value)
    {
        if (!_settingPrice && !_loading)
            _priceEdited = true;
    }

    protected override async Task OnInitializeAsync()
    {
        var companies = await Call<IReferenceLookups, IReadOnlyList<LookupOption>>(s => s.GetCompaniesAsync());
        var services = await Call<IReferenceLookups, IReadOnlyList<ServiceOption>>(s => s.GetServicesAsync());

        Companies.Clear();
        Companies.Add(AllCompanies);
        foreach (var company in companies)
            Companies.Add(company);

        ServiceOptions.Clear();
        foreach (var service in services)
            ServiceOptions.Add(service);
    }

    protected override PackageInput BuildInput() => new(
        Id, PackageName, PackageDescription, Price,
        CompanyChoiceId == 0 ? null : CompanyChoiceId,
        IsActive,
        [.. Services.Select(s => new PackageServiceLine(s.Id, s.ServiceId, s.Price))],
        RowVersion);

    protected override void ShowFields(PackageDetails d)
    {
        _loading = true;
        try
        {
            PackageName = d.PackageName;
            PackageDescription = d.PackageDescription;
            Price = d.Price;
            CompanyChoiceId = d.CompanyId ?? 0;
            IsActive = d.IsActive;
            RowVersion = d.RowVersion;
            SetServices(d.Services.Select(s => new PackageServiceLineViewModel
            {
                Id = s.Id,
                ServiceId = s.ServiceId,
                ServiceName = s.ServiceName ?? $"Service {s.ServiceId}",
                Price = s.Price,
            }));
            _priceEdited = d.Price != Services.Sum(s => s.Price);
        }
        finally
        {
            _loading = false;
        }
    }

    protected override void ResetFields()
    {
        _loading = true;
        try
        {
            PackageName = PackageDescription = string.Empty;
            Price = 0;
            CompanyChoiceId = 0;
            SetServices([]);
            _priceEdited = false;
        }
        finally
        {
            _loading = false;
        }
    }

    /// <summary>Opens the service picker with the current services ticked; Ok adds the newly ticked and removes the unticked.</summary>
    [RelayCommand]
    private void ChooseServices()
    {
        if (servicePicker.Pick(ServiceOptions, Services.Select(s => s.ServiceId)) is { } chosen)
            ApplyServiceSelection(chosen);
    }

    /// <summary>Services already in the package keep the price they were given; new ones come in at their standard price.</summary>
    private void ApplyServiceSelection(IReadOnlyList<long> chosen)
    {
        var ticked = chosen.ToHashSet();
        var offered = ServiceOptions.Select(o => o.Id).ToHashSet();

        // A service that has since been switched off is not in the picker, so it can not have been unticked there.
        foreach (var line in Services.Where(s => offered.Contains(s.ServiceId) && !ticked.Contains(s.ServiceId)).ToList())
        {
            Detach(line);
            Services.Remove(line);
        }

        foreach (var option in ServiceOptions.Where(o => ticked.Contains(o.Id) && Services.All(s => s.ServiceId != o.Id)))
        {
            var line = new PackageServiceLineViewModel { ServiceId = option.Id, ServiceName = option.Name, Price = option.Price };
            Attach(line);
            Services.Add(line);
        }

        Recalculate();
    }

    [RelayCommand]
    private void RemoveService(PackageServiceLineViewModel? line)
    {
        if (line is null)
            return;

        Detach(line);
        Services.Remove(line);
        Recalculate();
    }

    private void SetServices(IEnumerable<PackageServiceLineViewModel> lines)
    {
        foreach (var existing in Services)
            Detach(existing);
        Services.Clear();

        foreach (var line in lines)
        {
            Attach(line);
            Services.Add(line);
        }
    }

    private void Attach(PackageServiceLineViewModel line) => line.PropertyChanged += OnServiceLineChanged;

    private void Detach(PackageServiceLineViewModel line) => line.PropertyChanged -= OnServiceLineChanged;

    private void OnServiceLineChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_loading && e.PropertyName == nameof(PackageServiceLineViewModel.Price))
            Recalculate();
    }

    private void Recalculate()
    {
        if (_priceEdited || _loading)
            return;

        _settingPrice = true;
        try
        {
            Price = Services.Sum(s => s.Price);
        }
        finally
        {
            _settingPrice = false;
        }
    }
}
