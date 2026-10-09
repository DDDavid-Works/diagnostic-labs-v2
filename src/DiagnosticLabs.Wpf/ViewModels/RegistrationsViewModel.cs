using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Application.Entries;
using DiagnosticLabs.Application.Lookups;
using DiagnosticLabs.Application.Patients;
using DiagnosticLabs.Application.Registrations;
using DiagnosticLabs.Domain.Patients;
using DiagnosticLabs.Wpf.Services;
using Microsoft.Extensions.Logging;

namespace DiagnosticLabs.Wpf.ViewModels;

public partial class RegistrationServiceLineViewModel : ObservableObject
{
    public long Id { get; init; }

    public long ServiceId { get; init; }

    public string ServiceName { get; init; } = string.Empty;

    [ObservableProperty]
    private decimal _price;
}

/// <summary>
/// Patient registration: one form that records the registration, the patient (new or existing) and the services,
/// built to be quick to fill in at the counter (Ctrl+S saves, Save &amp; New starts the next patient).
/// </summary>
public partial class RegistrationsViewModel(
    IServiceRunner runner,
    IDialogService dialogs,
    ICurrentUser user,
    IServicePickerDialog servicePicker,
    IEntryBuilderDialog entryBuilder,
    INavigationService navigation,
    ILogger<RegistrationsViewModel> logger)
    : CrudViewModel<IRegistrationService, RegistrationListItem, RegistrationDetails, RegistrationInput>(
        runner, dialogs, user, ModuleIds.PatientRegistrations, "Patient Registration", "Registration", hasActiveFlag: false, logger)
{
    private const string NewCodeText = "(assigned when saved)";
    private const string NoCompanyCodeText = "(set the company code first)";

    private static readonly LookupOption NoCompany = new(0, "(no company)");
    private static readonly LookupOption AnyCompany = new(0, "(any company)");
    private static readonly PackageOption NoPackage = new(0, "(none)", 0);

    private readonly ILogger _log = logger;

    // While true, field changes come from loading or resetting the form and must not trigger lookups.
    private bool _loading;

    // The amount follows the services total until the user types one or a package sets it.
    private bool _priceEdited;
    private bool _settingPrice;
    private int _suggestVersion;
    private int _listVersion;
    private byte[]? _patientRowVersion;

    // ----- registration -----
    [ObservableProperty]
    private string _registrationCode = NewCodeText;

    [ObservableProperty]
    private DateTime? _date = DateTime.Today;

    [ObservableProperty]
    private long _companyChoiceId;

    [ObservableProperty]
    private string? _batchName;

    // Nullable because a combo box writes null into its selection while its items are being swapped.
    [ObservableProperty]
    private long? _packageChoiceId = 0;

    [ObservableProperty]
    private decimal _amountDue;

    // ----- patient -----
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsExistingPatient), nameof(CanChangePatient))]
    private long _patientId;

    [ObservableProperty]
    private string _patientCode = NewCodeText;

    [ObservableProperty]
    private string _patientName = string.Empty;

    [ObservableProperty]
    private DateTime? _dateOfBirth;

    [ObservableProperty]
    private string _age = string.Empty;

    [ObservableProperty]
    private string? _sex;

    [ObservableProperty]
    private string? _civilStatus;

    [ObservableProperty]
    private string _address = string.Empty;

    [ObservableProperty]
    private string? _contactNumbers;

    [ObservableProperty]
    private PatientSuggestion? _selectedSuggestion;

    // ----- list filters -----
    [ObservableProperty]
    private long _companyFilterId;

    [ObservableProperty]
    private DateTime? _dateFilter;

    // ----- lookups -----
    public ObservableCollection<LookupOption> Companies { get; } = [];

    public ObservableCollection<LookupOption> FilterCompanies { get; } = [];

    public ObservableCollection<PackageOption> Packages { get; } = [NoPackage];

    public ObservableCollection<string> Batches { get; } = [];

    public ObservableCollection<string> Genders { get; } = [];

    public ObservableCollection<string> CivilStatuses { get; } = [];

    public ObservableCollection<ServiceOption> ServiceOptions { get; } = [];

    public ObservableCollection<PatientSuggestion> Suggestions { get; } = [];

    public ObservableCollection<RegistrationServiceLineViewModel> Services { get; } = [];

    public bool HasSuggestions => Suggestions.Count > 0;

    public bool IsExistingPatient => PatientId != 0;

    /// <summary>A patient picked for a new registration can be swapped for a different or new one; a saved registration keeps its patient.</summary>
    public bool CanChangePatient => IsNew && IsExistingPatient;

    /// <summary>The age box is computed (read-only) while a birth date is entered, and free text otherwise.</summary>
    public bool IsAgeReadOnly => DateOfBirth is not null;

    public decimal ServicesTotal => Services.Sum(s => s.Price);

    public bool CanEditLists => CurrentUser.Can(ModuleIds.PatientRegistrations, ModuleAction.Edit);

    protected override string CurrentName => RegistrationCode;

    // The code is what uniquely finds the new registration in the list.
    protected override string SearchTextAfterCreate => RegistrationCode;

    /// <summary>"Pay now" is for saved registrations, and only for people who may record payments.</summary>
    public bool CanPayNow => !IsNew && CurrentUser.Can(ModuleIds.Payments, ModuleAction.Create);

    protected override void OnRecordChanged()
    {
        OnPropertyChanged(nameof(CanChangePatient));
        OnPropertyChanged(nameof(CanPayNow));
    }

    // ------------------------------------------------------------------ reactions

    partial void OnPatientNameChanged(string value)
    {
        if (!_loading && PatientId == 0)
            Fire(() => SuggestAsync(value));
    }

    partial void OnDateOfBirthChanged(DateTime? value)
    {
        OnPropertyChanged(nameof(IsAgeReadOnly));

        // With a birth date the age is computed; clearing it leaves the last text in place to be corrected by hand.
        if (value is { } d)
            Age = AgeCalculator.Describe(DateOnly.FromDateTime(d), DateOnly.FromDateTime(DateTime.Today)) ?? string.Empty;
    }

    partial void OnDateChanged(DateTime? value)
    {
        if (!_loading && IsNew)
            Fire(RefreshPreviewAsync);
    }

    partial void OnCompanyChoiceIdChanged(long value)
    {
        if (_loading)
            return;

        // Packages and batches belong to a company, so a different company starts again from "no package".
        _loading = true;
        try
        {
            PackageChoiceId = 0;
        }
        finally
        {
            _loading = false;
        }

        Fire(() => LoadCompanyListsAsync(value == 0 ? null : value, 0));
    }

    partial void OnPackageChoiceIdChanged(long? value)
    {
        if (!_loading && value is > 0)
            Fire(() => ApplyPackageAsync(value.Value));
    }

    partial void OnAmountDueChanged(decimal value)
    {
        if (!_settingPrice && !_loading)
            _priceEdited = true;
    }

    partial void OnSelectedSuggestionChanged(PatientSuggestion? value)
    {
        if (value is not null)
            Fire(() => UsePatientAsync(value.Id));
    }

    partial void OnCompanyFilterIdChanged(long value) => SearchAgain();

    partial void OnDateFilterChanged(DateTime? value) => SearchAgain();

    private void SearchAgain()
    {
        if (!_loading)
            Fire(() => SearchCommand.ExecuteAsync(null));
    }

    protected override CrudSearch CreateSearch() => new RegistrationSearch(
        SearchText,
        CompanyFilterId == 0 ? null : CompanyFilterId,
        DateFilter is { } d ? DateOnly.FromDateTime(d) : null,
        Page,
        PageSize,
        IncludeInactive);

    // ------------------------------------------------------------------ the form <-> the service

    protected override RegistrationInput BuildInput() => new(
        Id,
        DateOnly.FromDateTime(Date ?? DateTime.Today),
        CompanyChoiceId == 0 ? null : CompanyChoiceId,
        BatchName,
        PackageChoiceId is null or 0 ? null : PackageChoiceId,
        PatientId,
        PatientName,
        DateOfBirth is { } d ? DateOnly.FromDateTime(d) : null,
        Age,
        Sex,
        CivilStatus,
        Address,
        ContactNumbers,
        _patientRowVersion,
        AmountDue,
        [.. Services.Select(s => new RegistrationServiceLine(s.Id, s.ServiceId, s.Price))],
        RowVersion);

    protected override void ShowFields(RegistrationDetails d)
    {
        _loading = true;
        try
        {
            RegistrationCode = d.RegistrationCode;
            Date = d.Date.ToDateTime(TimeOnly.MinValue);
            CompanyChoiceId = d.CompanyId ?? 0;
            BatchName = d.BatchName;
            PackageChoiceId = d.PackageId ?? 0;
            PatientId = d.PatientId;
            PatientCode = d.PatientCode;
            PatientName = d.PatientName;
            DateOfBirth = d.DateOfBirth?.ToDateTime(TimeOnly.MinValue);
            Age = d.DateOfBirth is null ? d.Age ?? string.Empty : Age;
            Sex = d.Sex;
            CivilStatus = d.CivilStatus;
            Address = d.Address;
            ContactNumbers = d.ContactNumbers;
            _patientRowVersion = d.PatientRowVersion;
            RowVersion = d.RowVersion;
            SetServices(d.Services.Select(s => new RegistrationServiceLineViewModel
            {
                Id = s.Id,
                ServiceId = s.ServiceId,
                ServiceName = s.ServiceName ?? $"Service {s.ServiceId}",
                Price = s.Price,
            }));
            AmountDue = d.AmountDue;
            _priceEdited = d.AmountDue != ServicesTotal;
            ClearSuggestions();
        }
        finally
        {
            _loading = false;
        }

        Fire(() => LoadCompanyListsAsync(d.CompanyId, d.PackageId ?? 0));
    }

    protected override void ResetFields()
    {
        _loading = true;
        try
        {
            RegistrationCode = NewCodeText;
            Date = DateTime.Today;
            CompanyChoiceId = 0;
            BatchName = null;
            PackageChoiceId = 0;
            ClearPatient();
            SetServices([]);
            AmountDue = 0;
            _priceEdited = false;
            ClearSuggestions();
        }
        finally
        {
            _loading = false;
        }

        Fire(RefreshPreviewAsync);
        Fire(() => LoadCompanyListsAsync(null, 0));
    }

    protected override string SavedMessage(RegistrationDetails saved, bool wasNew) =>
        wasNew ? $"Saved. Registration code {saved.RegistrationCode}." : "Saved successfully.";

    protected override async Task OnInitializeAsync()
    {
        var companies = await Call<IReferenceLookups, IReadOnlyList<LookupOption>>(s => s.GetCompaniesAsync());
        var services = await Call<IReferenceLookups, IReadOnlyList<ServiceOption>>(s => s.GetServicesAsync());

        Companies.Clear();
        Companies.Add(NoCompany);
        FilterCompanies.Clear();
        FilterCompanies.Add(AnyCompany);
        foreach (var company in companies)
        {
            Companies.Add(company);
            FilterCompanies.Add(company);
        }

        ServiceOptions.Clear();
        foreach (var service in services)
            ServiceOptions.Add(service);

        await LoadChoicesAsync();
    }

    // ------------------------------------------------------------------ patient

    private void ClearPatient()
    {
        PatientId = 0;
        PatientCode = NewCodeText;
        PatientName = string.Empty;
        DateOfBirth = null;
        Age = string.Empty;
        Sex = null;
        CivilStatus = null;
        Address = string.Empty;
        ContactNumbers = null;
        _patientRowVersion = null;
    }

    /// <summary>Uses the patient clicked in the type-ahead list.</summary>
    [RelayCommand]
    private void PickSuggestion(PatientSuggestion? suggestion)
    {
        if (suggestion is not null)
            Fire(() => UsePatientAsync(suggestion.Id));
    }

    /// <summary>Lets go of the picked patient so a different (or brand new) one can be typed in.</summary>
    [RelayCommand]
    private void ChangePatient()
    {
        _loading = true;
        try
        {
            ClearPatient();
        }
        finally
        {
            _loading = false;
        }

        Fire(RefreshPreviewAsync);
    }

    private async Task SuggestAsync(string text)
    {
        var version = ++_suggestVersion;
        if (text.Trim().Length < RegistrationService.MinSuggestionLength)
        {
            ClearSuggestions();
            return;
        }

        await Task.Delay(250); // wait for a pause in typing
        if (version != _suggestVersion)
            return;

        var result = await Call<IRegistrationService, Result<IReadOnlyList<PatientSuggestion>>>(s => s.SuggestPatientsAsync(text));
        if (version != _suggestVersion || PatientId != 0)
            return;

        Suggestions.Clear();
        if (result.IsSuccess)
        {
            foreach (var suggestion in result.Value)
                Suggestions.Add(suggestion);
        }

        OnPropertyChanged(nameof(HasSuggestions));
    }

    private void ClearSuggestions()
    {
        _suggestVersion++;
        Suggestions.Clear();
        OnPropertyChanged(nameof(HasSuggestions));
    }

    private async Task UsePatientAsync(long patientId)
    {
        var result = await Call<IRegistrationService, Result<PatientDetails>>(s => s.GetPatientAsync(patientId));
        ClearSuggestions();
        SelectedSuggestion = null;
        if (result.IsFailure)
        {
            ShowError(result.Error.Message);
            return;
        }

        var p = result.Value;
        _loading = true;
        try
        {
            PatientId = p.Id;
            PatientCode = p.PatientCode;
            PatientName = p.PatientName;
            DateOfBirth = p.DateOfBirth?.ToDateTime(TimeOnly.MinValue);
            if (DateOfBirth is null)
                Age = p.Age ?? string.Empty;
            Sex = p.Sex;
            CivilStatus = p.CivilStatus;
            Address = p.Address;
            ContactNumbers = p.ContactNumbers;
            _patientRowVersion = p.RowVersion;
        }
        finally
        {
            _loading = false;
        }

        ClearMessage();
    }

    // ------------------------------------------------------------------ company, package, batch

    private async Task LoadCompanyListsAsync(long? companyId, long keepPackageId)
    {
        var version = ++_listVersion;
        var packages = await Call<IReferenceLookups, IReadOnlyList<PackageOption>>(s => s.GetPackagesAsync(companyId));
        var batches = await Call<IReferenceLookups, IReadOnlyList<string>>(s => s.GetBatchesAsync(companyId));
        if (version != _listVersion)
            return;

        // Swapping the list items clears the combo boxes' selections, so put the values back afterwards.
        var batch = BatchName;
        _loading = true;
        try
        {
            Packages.Clear();
            Packages.Add(NoPackage);
            foreach (var package in packages)
                Packages.Add(package);

            Batches.Clear();
            foreach (var name in batches)
                Batches.Add(name);

            PackageChoiceId = keepPackageId;
            BatchName = batch;
        }
        finally
        {
            _loading = false;
        }
    }

    /// <summary>A package replaces the services with its own (at the package prices) and sets the amount to the package price.</summary>
    private async Task ApplyPackageAsync(long packageId)
    {
        var services = await Call<IReferenceLookups, IReadOnlyList<ServiceOption>>(s => s.GetPackageServicesAsync(packageId));
        if (PackageChoiceId != packageId)
            return;

        SetServices(services.Select(s => new RegistrationServiceLineViewModel { ServiceId = s.Id, ServiceName = s.Name, Price = s.Price }));
        SetAmount(Packages.FirstOrDefault(p => p.Id == packageId)?.Price ?? ServicesTotal);
        _priceEdited = true;
    }

    // ------------------------------------------------------------------ services & amount

    /// <summary>Opens the service picker with the current services ticked; Ok adds the newly ticked and removes the unticked.</summary>
    [RelayCommand]
    private void ChooseServices()
    {
        if (servicePicker.Pick(ServiceOptions, Services.Select(s => s.ServiceId)) is { } chosen)
            ApplyServiceSelection(chosen);
    }

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
            var line = new RegistrationServiceLineViewModel { ServiceId = option.Id, ServiceName = option.Name, Price = option.Price };
            Attach(line);
            Services.Add(line);
        }

        Recalculate();
    }

    [RelayCommand]
    private void RemoveService(RegistrationServiceLineViewModel? line)
    {
        if (line is null)
            return;

        Detach(line);
        Services.Remove(line);
        Recalculate();
    }

    private void SetServices(IEnumerable<RegistrationServiceLineViewModel> lines)
    {
        foreach (var existing in Services)
            Detach(existing);
        Services.Clear();

        foreach (var line in lines)
        {
            Attach(line);
            Services.Add(line);
        }

        OnPropertyChanged(nameof(ServicesTotal));
    }

    private void Attach(RegistrationServiceLineViewModel line) => line.PropertyChanged += OnServiceLineChanged;

    private void Detach(RegistrationServiceLineViewModel line) => line.PropertyChanged -= OnServiceLineChanged;

    private void OnServiceLineChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_loading && e.PropertyName == nameof(RegistrationServiceLineViewModel.Price))
            Recalculate();
    }

    private void Recalculate()
    {
        OnPropertyChanged(nameof(ServicesTotal));
        if (!_priceEdited && !_loading)
            SetAmount(ServicesTotal);
    }

    private void SetAmount(decimal value)
    {
        _settingPrice = true;
        try
        {
            AmountDue = value;
        }
        finally
        {
            _settingPrice = false;
        }
    }

    // ------------------------------------------------------------------ code preview

    private async Task RefreshPreviewAsync()
    {
        if (!IsNew)
            return;

        var date = DateOnly.FromDateTime(Date ?? DateTime.Today);
        var result = await Call<IRegistrationService, Result<RegistrationPreview>>(s => s.PreviewAsync(date));
        if (result.IsFailure || !IsNew)
            return;

        RegistrationCode = result.Value.RegistrationCode ?? NoCompanyCodeText;
        if (PatientId == 0)
            PatientCode = result.Value.PatientCode;
    }

    // ------------------------------------------------------------------ commands

    /// <summary>Opens Payments with this registration loaded.</summary>
    [RelayCommand]
    private void PayNow()
    {
        if (!IsNew)
            navigation.OpenPayment(Id);
    }

    /// <summary>Saves, then shows an empty form ready for the next patient.</summary>
    [RelayCommand]
    private Task SaveAndNewAsync() => RunAsync(async () =>
    {
        if (await SaveCoreAsync())
            BeginNewRecord(keepMessage: true);
    });

    [RelayCommand(CanExecute = nameof(CanEditLists))]
    private Task EditGenderListAsync() => EditListAsync(EntryFields.Gender);

    [RelayCommand(CanExecute = nameof(CanEditLists))]
    private Task EditCivilStatusListAsync() => EditListAsync(EntryFields.CivilStatus);

    [RelayCommand]
    private void ClearFilters()
    {
        _loading = true;
        try
        {
            SearchText = string.Empty;
            CompanyFilterId = 0;
            DateFilter = null;
        }
        finally
        {
            _loading = false;
        }

        SearchAgain();
    }

    private Task EditListAsync(EntryField field) => RunAsync(async () =>
    {
        if (entryBuilder.EditSingleLine(field, ModuleIds.PatientRegistrations))
            await LoadChoicesAsync();
    });

    private async Task LoadChoicesAsync()
    {
        var genders = await Call<IEntryService, IReadOnlyList<string>>(s => s.GetChoicesAsync(EntryFields.Gender));
        var statuses = await Call<IEntryService, IReadOnlyList<string>>(s => s.GetChoicesAsync(EntryFields.CivilStatus));
        Replace(Genders, genders);
        Replace(CivilStatuses, statuses);
    }

    private static void Replace(ObservableCollection<string> target, IEnumerable<string> values)
    {
        target.Clear();
        foreach (var value in values)
            target.Add(value);
    }

    /// <summary>Runs a background lookup without blocking the form; a failure is logged and the form keeps working.</summary>
    private async void Fire(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Background lookup failed on the registration screen");
        }
    }
}
