using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Application.Entries;
using DiagnosticLabs.Application.LabResults;
using DiagnosticLabs.Application.Lookups;
using DiagnosticLabs.Application.Registrations;
using DiagnosticLabs.Wpf.Services;
using Microsoft.Extensions.Logging;

namespace DiagnosticLabs.Wpf.ViewModels;

/// <summary>A result field that is picked from a maintained list (or typed): its value, its list and the "Edit entries..." command.</summary>
public sealed partial class ChoiceField(string name, string label, EntryField entry) : ObservableObject
{
    /// <summary>The key used for saved defaults.</summary>
    public string Name { get; } = name;

    /// <summary>What the printed form calls it.</summary>
    public string Label { get; } = label;

    public EntryField Entry { get; } = entry;

    public ObservableCollection<string> Items { get; } = [];

    [ObservableProperty]
    private string? _value;

    [ObservableProperty]
    private System.Windows.Input.ICommand? _editCommand;
}

/// <summary>One row of a two-column result table; the right side may be empty.</summary>
public sealed record ChoiceRow(ChoiceField Left, ChoiceField? Right);

/// <summary>One field of a result form that can have a default value: its name in the saved defaults and how to read and set it.</summary>
public sealed class DefaultField(string name, Func<string?> get, Action<string?> set)
{
    public string Name { get; } = name;

    public string? Get() => get();

    public void Set(string? value) => set(value);
}

/// <summary>A dropdown of reusable texts (the multi-line Entry Builder lists); picking one fills a text box.</summary>
public sealed partial class TemplateOptions : ObservableObject
{
    /// <summary>What happens when a text is picked (the screen sets this to fill its text box).</summary>
    public Action<string>? Apply { get; set; }

    public ObservableCollection<MultiLineEntry> Items { get; } = [];

    [ObservableProperty]
    private MultiLineEntry? _selected;

    partial void OnSelectedChanged(MultiLineEntry? value)
    {
        if (value is null)
            return;

        Apply?.Invoke(value.Text);

        // Clear the pick so the same text can be chosen again. It is done after the dropdown has finished
        // changing its selection; clearing it in the middle of that leaves the old title showing in the box.
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null)
            Selected = null;
        else
            dispatcher.BeginInvoke(() => Selected = null);
    }

    public void Replace(IEnumerable<MultiLineEntry> entries)
    {
        Items.Clear();
        foreach (var entry in entries)
            Items.Add(entry);
    }
}

/// <summary>
/// What every lab result screen shares: the registration box at the top (optional: a result can be printed for someone who is not
/// registered), the patient block that the registration fills in, the remarks, the two signatories, Save &amp; New and Print.
/// A concrete screen adds its own result fields.
/// </summary>
public abstract partial class LabResultViewModel<TService, TDetails, TInput>(
    IServiceRunner runner,
    IDialogService dialogs,
    ICurrentUser user,
    IEntryBuilderDialog entryBuilder,
    IReportPreviewDialog preview,
    int moduleId,
    string title,
    ILogger logger)
    : CrudViewModel<TService, LabResultListItem, TDetails, TInput>(runner, dialogs, user, moduleId, title, title, hasActiveFlag: false, logger)
    where TService : ICrudService<LabResultListItem, TDetails, TInput>, ILabResultPrinting
    where TDetails : IHasId
    where TInput : ICrudInput
{
    private readonly int _module = moduleId;
    private readonly IDialogService _dialogs = dialogs;
    private readonly ILogger _log = logger;

    // While true, field changes come from loading or resetting the form and must not trigger lookups.
    private bool _loading;
    private bool _confirmedDuplicate;
    private int _suggestVersion;
    private int _registrationVersion;

    // ----- the registration (optional) -----
    [ObservableProperty]
    private string _findText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRegistration), nameof(IsPatientLocked), nameof(RegistrationCaption), nameof(CanLookUpPatient))]
    private ResultRegistration? _registration;

    [ObservableProperty]
    private ResultRegistrationMatch? _selectedSuggestion;

    // A result without a registration can still be tied to a patient picked from the patient list.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPatient), nameof(IsPatientLocked), nameof(CanLookUpPatient))]
    private long? _patientId;

    // ----- the patient block (a snapshot saved with the result) -----
    [ObservableProperty]
    private string _patientCode = string.Empty;

    [ObservableProperty]
    private string _patientName = string.Empty;

    [ObservableProperty]
    private string? _age;

    [ObservableProperty]
    private string? _sex;

    [ObservableProperty]
    private string? _companyOrPhysician;

    [ObservableProperty]
    private DateTime? _dateRequested = DateTime.Today;

    // ----- the end of the form -----
    [ObservableProperty]
    private string? _remarks;

    [ObservableProperty]
    private string? _medicalTechnologist;

    [ObservableProperty]
    private string? _pathologist;

    [ObservableProperty]
    private ImageSource? _photo;

    // ----- list filter -----
    [ObservableProperty]
    private DateTime? _dateFilter;

    // ----- defaults: the values a new result starts with (shared by everyone, set by administrators) -----
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotSettingDefaults), nameof(ShowDefaultsButton), nameof(CanResetToDefaults), nameof(CanPrint))]
    private bool _isSettingDefaults;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanResetToDefaults))]
    private bool _hasDefaults;

    private IReadOnlyDictionary<string, string?> _defaults = new Dictionary<string, string?>();
    private bool _enteringDefaults;

    public ObservableCollection<ResultRegistrationMatch> Suggestions { get; } = [];

    public ObservableCollection<string> Genders { get; } = [];

    public ObservableCollection<string> Technologists { get; } = [];

    public ObservableCollection<string> Pathologists { get; } = [];

    public ObservableCollection<string> CompanyNames { get; } = [];

    // Created up front so the screen can bind to it before the lists have loaded.
    public TemplateOptions RemarksTemplates { get; } = new();

    public ObservableCollection<PatientSuggestion> PatientSuggestions { get; } = [];

    public bool HasPatientSuggestions => PatientSuggestions.Count > 0;

    public bool HasSuggestions => Suggestions.Count > 0;

    public bool HasPatient => PatientId is not null;

    /// <summary>Patients can be looked up only while no registration is loaded (a registration brings its own patient) and none is picked yet.</summary>
    public bool CanLookUpPatient => !HasRegistration && !HasPatient;

    public bool HasRegistration => Registration is not null;

    /// <summary>With a registration (or a picked patient) the patient code and name come from there and stay as they are; a stray result is typed in.</summary>
    public bool IsPatientLocked => HasRegistration || HasPatient;

    public string RegistrationCaption => Registration is null
        ? string.Empty
        : $"{Registration.RegistrationCode} - {Registration.Date:d}" + (Registration.Services.Count > 0 ? $" - {string.Join(", ", Registration.Services)}" : string.Empty);

    public bool CanEditLists => CurrentUser.Can(_module, ModuleAction.Edit);

    public bool CanPrint => CurrentUser.Can(_module, ModuleAction.Print) && !IsSettingDefaults;

    public bool IsNotSettingDefaults => !IsSettingDefaults;

    /// <summary>Only administrators set the defaults.</summary>
    public bool CanSetDefaults => CurrentUser.IsAdmin;

    public bool ShowDefaultsButton => CanSetDefaults && !IsSettingDefaults;

    /// <summary>Resetting is for a new, unsaved result; a saved one is being edited and keeps what it has.</summary>
    public bool CanResetToDefaults => IsNew && !IsSettingDefaults && HasDefaults;

    public string DefaultsBanner => $"Setting the defaults for {Title}. Registration and patient details are not part of the defaults.";

    // The form does not save results while the defaults are being set.
    protected override bool IsLocked => IsSettingDefaults;

    protected override string CurrentName => string.IsNullOrWhiteSpace(PatientName) ? "this result" : $"the result of {PatientName}";

    /// <summary>The list that holds the reusable Remarks texts of this screen.</summary>
    protected abstract EntryField RemarksField { get; }

    protected abstract Task<Result<PrintableReport>> GetPrintableAsync(long id);

    /// <summary>The result fields of this screen that can have a default (Remarks and the signatories are added for every screen).</summary>
    protected virtual IEnumerable<DefaultField> ExtraDefaultFields() => [];

    protected abstract void ResetDetail();

    /// <summary>Called when a registration or patient fills the patient block, so a screen can take over more of their details (birth date, civil status, contact).</summary>
    protected virtual void OnPersonPicked(DateOnly? dateOfBirth, string? civilStatus, string? contactNumbers)
    {
    }

    /// <summary>The list-picked fields of this screen. They load their lists, reset, take defaults and get their "Edit entries..." command from here.</summary>
    protected virtual IEnumerable<ChoiceField> ChoiceFields => [];

    private List<DefaultField> DefaultFields() =>
    [
        new("Remarks", () => Remarks, v => Remarks = v),
        new("MedicalTechnologist", () => MedicalTechnologist, v => MedicalTechnologist = v),
        new("Pathologist", () => Pathologist, v => Pathologist = v),
        .. ChoiceFields.Select(c => new DefaultField(c.Name, () => c.Value, v => c.Value = v)),
        .. ExtraDefaultFields(),
    ];

    // A new form is the empty form plus the saved defaults.
    protected sealed override void ResetFields()
    {
        ResetHeader();
        foreach (var choice in ChoiceFields)
            choice.Value = null;

        ResetDetail();
        ApplyDefaults();
    }

    private void ApplyDefaults()
    {
        foreach (var field in DefaultFields())
        {
            if (_defaults.TryGetValue(field.Name, out var value))
                field.Set(value);
        }
    }

    protected ILogger Log => _log;

    protected int ModuleId => _module;

    // ------------------------------------------------------------------ header <-> service

    protected LabResultHeader BuildHeader() => new(
        Registration?.RegistrationId,
        Registration?.RegistrationCode,
        PatientCode,
        PatientName,
        Age,
        Sex,
        CompanyOrPhysician,
        DateOnly.FromDateTime(DateRequested ?? DateTime.Today),
        Remarks,
        MedicalTechnologist,
        Pathologist,
        _confirmedDuplicate,
        HasRegistration ? null : PatientId);

    /// <summary>Shows a loaded result's header; the registration's own details are fetched in the background.</summary>
    protected void ShowHeader(LabResultHeader header, byte[]? photo)
    {
        _registrationVersion++;
        _loading = true;
        try
        {
            Registration = null;
            PatientId = header.RegistrationId is null ? header.PatientId : null;
            FindText = header.RegistrationCode ?? string.Empty;
            PatientCode = header.PatientCode ?? string.Empty;
            PatientName = header.PatientName ?? string.Empty;
            Age = header.Age;
            Sex = header.Sex;
            CompanyOrPhysician = header.CompanyOrPhysician;
            DateRequested = header.DateRequested.ToDateTime(TimeOnly.MinValue);
            Remarks = header.Remarks;
            MedicalTechnologist = header.MedicalTechnologist;
            Pathologist = header.Pathologist;
            Photo = ImageLoader.FromBytes(photo);
            _confirmedDuplicate = false;
            ClearSuggestions();
            ClearPatientSuggestions();
        }
        finally
        {
            _loading = false;
        }

        if (header.RegistrationId is { } registrationId)
            Fire(() => LoadRegistrationAsync(registrationId, fillPatient: false));
    }

    protected void ResetHeader()
    {
        _registrationVersion++;
        _loading = true;
        try
        {
            Registration = null;
            PatientId = null;
            FindText = string.Empty;
            PatientCode = string.Empty;
            PatientName = string.Empty;
            Age = null;
            Sex = null;
            CompanyOrPhysician = null;
            DateRequested = DateTime.Today;
            Remarks = null;
            MedicalTechnologist = null;
            Pathologist = null;
            Photo = null;
            _confirmedDuplicate = false;
            ClearSuggestions();
            ClearPatientSuggestions();
        }
        finally
        {
            _loading = false;
        }
    }

    protected override async Task OnInitializeAsync()
    {
        await LoadListsAsync();
        await LoadTemplatesAsync();
        await LoadDefaultsAsync();
    }

    // Opening a record or starting a new one ends the "set defaults" mode (except when the mode itself starts the new form).
    protected override void OnRecordChanged()
    {
        if (IsSettingDefaults && !_enteringDefaults)
            ExitDefaultsMode();

        OnPropertyChanged(nameof(CanResetToDefaults));
    }

    protected override Task<bool> OnSaveRefusedAsync(Error error)
    {
        // A second result of the same kind for a registration is allowed, but only after the user says so.
        if (error.Code == "LabResult.Duplicate" && _dialogs.Confirm(error.Message, "Another result for this registration"))
        {
            _confirmedDuplicate = true;
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    // ------------------------------------------------------------------ the registration box

    partial void OnFindTextChanged(string value)
    {
        if (!_loading)
            Fire(() => SuggestAsync(value));
    }

    partial void OnDateFilterChanged(DateTime? value)
    {
        if (!_loading)
            Fire(() => SearchCommand.ExecuteAsync(null));
    }

    protected override CrudSearch CreateSearch() => new LabResultSearch(
        SearchText, DateFilter is { } d ? DateOnly.FromDateTime(d) : null, Page, PageSize, IncludeInactive);

    /// <summary>Loads the registration whose code was typed (Enter in the registration box).</summary>
    [RelayCommand]
    private Task FindAsync() => RunAsync(async () =>
    {
        var text = FindText;
        var result = await Call<ILabRegistrationLookup, Result<ResultRegistration>>(s => s.FindAsync(_module, text));
        if (result.IsFailure)
        {
            ShowError(result.Error.Message);
            return;
        }

        ClearSuggestions();
        ApplyRegistration(result.Value, fillPatient: true);
        ClearMessage();
    });

    /// <summary>Loads the registration clicked in the type-ahead list.</summary>
    [RelayCommand]
    private void PickSuggestion(ResultRegistrationMatch? match)
    {
        if (match is null)
            return;

        ClearSuggestions();
        Fire(() => LoadRegistrationAsync(match.Id, fillPatient: true));
    }

    /// <summary>Lets go of the registration so the patient block can be typed in (a stray result).</summary>
    [RelayCommand]
    private void ChangeRegistration()
    {
        _registrationVersion++;
        _loading = true;
        try
        {
            Registration = null;
            FindText = string.Empty;
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task LoadRegistrationAsync(long registrationId, bool fillPatient)
    {
        var version = ++_registrationVersion;
        var result = await Call<ILabRegistrationLookup, Result<ResultRegistration>>(s => s.GetAsync(_module, registrationId));
        if (version != _registrationVersion)
            return;

        if (result.IsFailure)
        {
            ShowError(result.Error.Message);
            return;
        }

        ApplyRegistration(result.Value, fillPatient);
        if (fillPatient)
            ClearMessage();
    }

    /// <summary>Shows a registration; for a new result it also fills the patient block (code, name, age, sex, company).</summary>
    private void ApplyRegistration(ResultRegistration r, bool fillPatient)
    {
        _loading = true;
        try
        {
            Registration = r;
            PatientId = null;
            ClearPatientSuggestions();
            FindText = r.RegistrationCode;
            if (fillPatient)
            {
                PatientCode = r.PatientCode;
                PatientName = r.PatientName;
                Age = r.Age;
                Sex = r.Sex;
                if (!string.IsNullOrWhiteSpace(r.CompanyName))
                    CompanyOrPhysician = r.CompanyName;
                OnPersonPicked(r.DateOfBirth, r.CivilStatus, r.ContactNumbers);
            }
        }
        finally
        {
            _loading = false;
        }
    }

    // ------------------------------------------------------------------ patient lookup (only without a registration)

    private int _patientSuggestVersion;

    partial void OnPatientNameChanged(string value)
    {
        if (!_loading && CanLookUpPatient)
            Fire(() => SuggestPatientsAsync(value));
    }

    partial void OnPatientCodeChanged(string value)
    {
        if (!_loading && CanLookUpPatient)
            Fire(() => SuggestPatientsAsync(value));
    }

    private async Task SuggestPatientsAsync(string text)
    {
        var version = ++_patientSuggestVersion;
        if (text.Trim().Length < LabRegistrationLookup.MinSuggestionLength)
        {
            ClearPatientSuggestions();
            return;
        }

        await Task.Delay(250); // wait for a pause in typing
        if (version != _patientSuggestVersion || !CanLookUpPatient)
            return;

        var result = await Call<ILabRegistrationLookup, Result<IReadOnlyList<PatientSuggestion>>>(s => s.SuggestPatientsAsync(_module, text));
        if (version != _patientSuggestVersion || !CanLookUpPatient)
            return;

        PatientSuggestions.Clear();
        if (result.IsSuccess)
        {
            foreach (var patient in result.Value)
                PatientSuggestions.Add(patient);
        }

        OnPropertyChanged(nameof(HasPatientSuggestions));
    }

    private void ClearPatientSuggestions()
    {
        _patientSuggestVersion++;
        PatientSuggestions.Clear();
        OnPropertyChanged(nameof(HasPatientSuggestions));
    }

    /// <summary>Fills the patient block from the patient clicked in the list and ties the result to them.</summary>
    [RelayCommand]
    private void PickPatient(PatientSuggestion? suggestion)
    {
        if (suggestion is null || !CanLookUpPatient)
            return;

        ClearPatientSuggestions();
        Fire(async () =>
        {
            var result = await Call<ILabRegistrationLookup, Result<ResultPatient>>(s => s.GetPatientAsync(_module, suggestion.Id));
            if (result.IsFailure)
            {
                ShowError(result.Error.Message);
                return;
            }

            var p = result.Value;
            _loading = true;
            try
            {
                PatientId = p.PatientId;
                PatientCode = p.PatientCode;
                PatientName = p.PatientName;
                Age = p.Age;
                Sex = p.Sex;
                OnPersonPicked(p.DateOfBirth, p.CivilStatus, p.ContactNumbers);
            }
            finally
            {
                _loading = false;
            }

            ClearMessage();
        });
    }

    /// <summary>Lets go of the picked patient so the block can be typed in again (or another patient looked up).</summary>
    [RelayCommand]
    private void ChangePatient()
    {
        PatientId = null;
        ClearPatientSuggestions();
    }

    private async Task SuggestAsync(string text)
    {
        var version = ++_suggestVersion;
        if (text.Trim().Length < LabRegistrationLookup.MinSuggestionLength)
        {
            ClearSuggestions();
            return;
        }

        await Task.Delay(250); // wait for a pause in typing
        if (version != _suggestVersion)
            return;

        var result = await Call<ILabRegistrationLookup, Result<IReadOnlyList<ResultRegistrationMatch>>>(s => s.SuggestAsync(_module, text));
        if (version != _suggestVersion)
            return;

        Suggestions.Clear();
        if (result.IsSuccess)
        {
            foreach (var match in result.Value)
                Suggestions.Add(match);
        }

        OnPropertyChanged(nameof(HasSuggestions));
    }

    private void ClearSuggestions()
    {
        _suggestVersion++;
        Suggestions.Clear();
        OnPropertyChanged(nameof(HasSuggestions));
    }

    // ------------------------------------------------------------------ lists and templates

    protected async Task LoadListsAsync()
    {
        var genders = await Call<IEntryService, IReadOnlyList<string>>(s => s.GetChoicesAsync(EntryFields.Gender));
        var technologists = await Call<IEntryService, IReadOnlyList<string>>(s => s.GetChoicesAsync(EntryFields.MedicalTechnologist));
        var pathologists = await Call<IEntryService, IReadOnlyList<string>>(s => s.GetChoicesAsync(EntryFields.Pathologist));
        var companies = await Call<IReferenceLookups, IReadOnlyList<LookupOption>>(s => s.GetCompaniesAsync());

        Replace(Genders, genders);
        Replace(Technologists, technologists);
        Replace(Pathologists, pathologists);
        Replace(CompanyNames, companies.Select(c => c.Name));

        foreach (var choice in ChoiceFields)
        {
            var field = choice;
            field.EditCommand ??= new AsyncRelayCommand(() => EditSingleLineAsync(field.Entry), () => CanEditLists);
            Replace(field.Items, await Call<IEntryService, IReadOnlyList<string>>(s => s.GetChoicesAsync(field.Entry)));
        }
    }

    protected async Task LoadTemplatesAsync()
    {
        RemarksTemplates.Apply ??= text => Remarks = text;
        var remarks = await Call<IEntryService, Result<MultiLineEntryList>>(s => s.GetMultiLineAsync(RemarksField, _module));
        RemarksTemplates.Replace(remarks.IsSuccess ? remarks.Value.Items : []);
    }

    protected static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> values)
    {
        target.Clear();
        foreach (var value in values)
            target.Add(value);
    }

    /// <summary>Opens the Entry Builder for a single-line list of this screen and reloads the dropdowns if it was saved.</summary>
    protected Task EditSingleLineAsync(EntryField field) => RunAsync(async () =>
    {
        if (entryBuilder.EditSingleLine(field, _module))
            await ReloadChoicesAsync();
    });

    protected Task EditMultiLineAsync(EntryField field) => RunAsync(async () =>
    {
        if (entryBuilder.EditMultiLine(field, _module))
            await ReloadChoicesAsync();
    });

    protected virtual async Task ReloadChoicesAsync()
    {
        await LoadListsAsync();
        await LoadTemplatesAsync();
    }

    [RelayCommand(CanExecute = nameof(CanEditLists))]
    private Task EditGenderListAsync() => EditSingleLineAsync(EntryFields.Gender);

    [RelayCommand(CanExecute = nameof(CanEditLists))]
    private Task EditTechnologistListAsync() => EditSingleLineAsync(EntryFields.MedicalTechnologist);

    [RelayCommand(CanExecute = nameof(CanEditLists))]
    private Task EditPathologistListAsync() => EditSingleLineAsync(EntryFields.Pathologist);

    [RelayCommand(CanExecute = nameof(CanEditLists))]
    private Task EditRemarksTemplatesAsync() => EditMultiLineAsync(RemarksField);

    // ------------------------------------------------------------------ Save & New, Print, filters

    // ------------------------------------------------------------------ defaults

    private async Task LoadDefaultsAsync()
    {
        var result = await Call<IModuleDefaultsService, Result<IReadOnlyDictionary<string, string?>>>(s => s.GetAsync(_module));
        _defaults = result.IsSuccess ? result.Value : new Dictionary<string, string?>();
        HasDefaults = _defaults.Count > 0;
    }

    private void ExitDefaultsMode()
    {
        IsSettingDefaults = false;
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(CanDelete));
        PrintCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Shows a new form carrying the current defaults to edit them; patient and registration are switched off.</summary>
    [RelayCommand(CanExecute = nameof(CanSetDefaults))]
    private void EnterDefaults()
    {
        _enteringDefaults = true;
        try
        {
            IsSettingDefaults = true;
            BeginNewRecord();
        }
        finally
        {
            _enteringDefaults = false;
        }

        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(CanDelete));
        PrintCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private Task SaveDefaultsAsync() => RunAsync(async () =>
    {
        var values = DefaultFields().ToDictionary(f => f.Name, f => f.Get());
        var result = await Call<IModuleDefaultsService, Result>(s => s.SaveAsync(_module, values));
        if (result.IsFailure)
        {
            ShowError(result.Error.Message);
            return;
        }

        await LoadDefaultsAsync();
        ExitDefaultsMode();
        BeginNewRecord(keepMessage: true);
        ShowInfo("Defaults saved. New results will start with them.");
    });

    [RelayCommand]
    private Task ClearDefaultsAsync() => RunAsync(async () =>
    {
        if (!_dialogs.Confirm($"Remove the defaults of {Title}? New results will start empty.", "Clear defaults"))
            return;

        var result = await Call<IModuleDefaultsService, Result>(s => s.ClearAsync(_module));
        if (result.IsFailure)
        {
            ShowError(result.Error.Message);
            return;
        }

        await LoadDefaultsAsync();
        ExitDefaultsMode();
        BeginNewRecord(keepMessage: true);
        ShowInfo("Defaults cleared.");
    });

    [RelayCommand]
    private void CancelDefaults()
    {
        ExitDefaultsMode();
        BeginNewRecord();
    }

    /// <summary>Puts the defaults back into the result fields of a new, unsaved result (patient and registration are left alone).</summary>
    [RelayCommand(CanExecute = nameof(CanResetToDefaults))]
    private void ResetToDefaults()
    {
        foreach (var field in DefaultFields())
            field.Set(null);

        ApplyDefaults();
    }

    /// <summary>Saves, then shows an empty form ready for the next result.</summary>
    [RelayCommand]
    private Task SaveAndNewAsync() => RunAsync(async () =>
    {
        if (await SaveCoreAsync())
            BeginNewRecord(keepMessage: true);
    });

    /// <summary>Saves what is on the form (so the paper matches the screen) and opens the print preview.</summary>
    [RelayCommand(CanExecute = nameof(CanPrint))]
    private Task PrintAsync() => RunAsync(async () =>
    {
        if (!await SaveCoreAsync())
            return;

        var result = await GetPrintableAsync(Id);
        if (result.IsFailure)
        {
            ShowError(result.Error.Message);
            return;
        }

        preview.Show(result.Value);
    });

    [RelayCommand]
    private void ClearFilters()
    {
        _loading = true;
        try
        {
            SearchText = string.Empty;
            DateFilter = null;
        }
        finally
        {
            _loading = false;
        }

        Fire(() => SearchCommand.ExecuteAsync(null));
    }

    /// <summary>Runs a background lookup without blocking the form; a failure is logged and the form keeps working.</summary>
    protected async void Fire(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Background lookup failed on a lab result screen");
        }
    }
}

public partial class StoolFecalysisViewModel(
    IServiceRunner runner,
    IDialogService dialogs,
    ICurrentUser user,
    IEntryBuilderDialog entryBuilder,
    IReportPreviewDialog preview,
    ILogger<StoolFecalysisViewModel> logger)
    : LabResultViewModel<IStoolFecalysisService, StoolFecalysisDetails, StoolFecalysisInput>(
        runner, dialogs, user, entryBuilder, preview, ModuleIds.StoolFecalysis, "Stool/Fecalysis", logger)
{
    [ObservableProperty]
    private string? _color;

    [ObservableProperty]
    private string? _consistency;

    [ObservableProperty]
    private string? _result;

    public ObservableCollection<string> Colors { get; } = [];

    public ObservableCollection<string> Consistencies { get; } = [];

    public TemplateOptions ResultTemplates { get; } = new();

    protected override EntryField RemarksField => EntryFields.StoolRemarks;

    protected override async Task OnInitializeAsync()
    {
        await base.OnInitializeAsync();
        await LoadStoolListsAsync();
    }

    protected override async Task ReloadChoicesAsync()
    {
        await base.ReloadChoicesAsync();
        await LoadStoolListsAsync();
    }

    private async Task LoadStoolListsAsync()
    {
        var colors = await Call<IEntryService, IReadOnlyList<string>>(s => s.GetChoicesAsync(EntryFields.StoolColor));
        var consistencies = await Call<IEntryService, IReadOnlyList<string>>(s => s.GetChoicesAsync(EntryFields.StoolConsistency));
        var results = await Call<IEntryService, Result<MultiLineEntryList>>(s => s.GetMultiLineAsync(EntryFields.StoolResult, ModuleId));

        Replace(Colors, colors);
        Replace(Consistencies, consistencies);
        ResultTemplates.Apply ??= text => Result = text;
        ResultTemplates.Replace(results.IsSuccess ? results.Value.Items : []);
    }

    protected override StoolFecalysisInput BuildInput() => new(Id, BuildHeader(), Color, Consistency, Result, RowVersion);

    protected override void ShowFields(StoolFecalysisDetails d)
    {
        ShowHeader(d.Header, d.Photo);
        Color = d.Color;
        Consistency = d.Consistency;
        Result = d.Result;
        RowVersion = d.RowVersion;
    }

    protected override void ResetDetail()
    {
        Color = null;
        Consistency = null;
        Result = null;
    }

    protected override IEnumerable<DefaultField> ExtraDefaultFields() =>
    [
        new("Color", () => Color, v => Color = v),
        new("Consistency", () => Consistency, v => Consistency = v),
        new("Result", () => Result, v => Result = v),
    ];

    protected override Task<Result<PrintableReport>> GetPrintableAsync(long id) =>
        Call<IStoolFecalysisService, Result<PrintableReport>>(s => s.GetPrintableAsync(id));

    [RelayCommand(CanExecute = nameof(CanEditLists))]
    private Task EditColorListAsync() => EditSingleLineAsync(EntryFields.StoolColor);

    [RelayCommand(CanExecute = nameof(CanEditLists))]
    private Task EditConsistencyListAsync() => EditSingleLineAsync(EntryFields.StoolConsistency);

    [RelayCommand(CanExecute = nameof(CanEditLists))]
    private Task EditResultTemplatesAsync() => EditMultiLineAsync(EntryFields.StoolResult);
}

public partial class UrinalysisViewModel(
    IServiceRunner runner,
    IDialogService dialogs,
    ICurrentUser user,
    IEntryBuilderDialog entryBuilder,
    IReportPreviewDialog preview,
    ILogger<UrinalysisViewModel> logger)
    : LabResultViewModel<IUrinalysisService, UrinalysisDetails, UrinalysisInput>(
        runner, dialogs, user, entryBuilder, preview, ModuleIds.Urinalysis, "Urinalysis", logger)
{
    public ChoiceField Color { get; } = new("Color", "Color", EntryFields.UrinalysisColor);

    public ChoiceField Appearance { get; } = new("Appearance", "Appearance", EntryFields.UrinalysisAppearance);

    public ChoiceField Reaction { get; } = new("Reaction", "Reaction", EntryFields.UrinalysisReaction);

    public ChoiceField SPGravity { get; } = new("SPGravity", "SP. Gravity", EntryFields.UrinalysisSpGravity);

    public ChoiceField Albumin { get; } = new("Albumin", "Albumin", EntryFields.UrinalysisAlbumin);

    public ChoiceField Sugar { get; } = new("Sugar", "Sugar", EntryFields.UrinalysisSugar);

    public ChoiceField PusCells { get; } = new("PusCells", "Pus Cells", EntryFields.UrinalysisPusCells);

    public ChoiceField RedCells { get; } = new("RedCells", "Red Cells", EntryFields.UrinalysisRedCells);

    public ChoiceField MucusThreads { get; } = new("MucusThreads", "Mucus Threads", EntryFields.UrinalysisMucusThreads);

    public ChoiceField EpithelialCells { get; } = new("EpithelialCells", "Epithelial Cells", EntryFields.UrinalysisEpithelialCells);

    public ChoiceField AmorphousUratesPO4 { get; } = new("AmorphousUratesPO4", "Amorphous Urates / PO4", EntryFields.UrinalysisAmorphousUrates);

    public ChoiceField Bacteria { get; } = new("Bacteria", "Bacteria", EntryFields.UrinalysisBacteria);

    public ChoiceField Casts { get; } = new("Casts", "Casts", EntryFields.UrinalysisCasts);

    public ChoiceField Crystals { get; } = new("Crystals", "Crystals", EntryFields.UrinalysisCrystals);

    [ObservableProperty]
    private string? _others;

    public TemplateOptions OthersTemplates { get; } = new();

    /// <summary>The table of the form, in the order of the printed form: two columns, the right one shorter.</summary>
    public IReadOnlyList<ChoiceRow> Rows =>
    [
        new(Color, MucusThreads),
        new(Appearance, EpithelialCells),
        new(Reaction, AmorphousUratesPO4),
        new(SPGravity, Bacteria),
        new(Albumin, Casts),
        new(Sugar, Crystals),
        new(PusCells, null),
        new(RedCells, null),
    ];

    protected override IEnumerable<ChoiceField> ChoiceFields =>
        [Color, Appearance, Reaction, SPGravity, Albumin, Sugar, PusCells, RedCells, MucusThreads, EpithelialCells, AmorphousUratesPO4, Bacteria, Casts, Crystals];

    protected override EntryField RemarksField => EntryFields.UrinalysisRemarks;

    protected override async Task OnInitializeAsync()
    {
        await base.OnInitializeAsync();
        await LoadOthersTemplatesAsync();
    }

    protected override async Task ReloadChoicesAsync()
    {
        await base.ReloadChoicesAsync();
        await LoadOthersTemplatesAsync();
    }

    private async Task LoadOthersTemplatesAsync()
    {
        var others = await Call<IEntryService, Result<MultiLineEntryList>>(s => s.GetMultiLineAsync(EntryFields.UrinalysisOthers, ModuleId));
        OthersTemplates.Apply ??= text => Others = text;
        OthersTemplates.Replace(others.IsSuccess ? others.Value.Items : []);
    }

    protected override UrinalysisInput BuildInput() => new(
        Id, BuildHeader(), Color.Value, Appearance.Value, Reaction.Value, SPGravity.Value, Albumin.Value, Sugar.Value, PusCells.Value,
        RedCells.Value, MucusThreads.Value, EpithelialCells.Value, AmorphousUratesPO4.Value, Bacteria.Value, Casts.Value, Crystals.Value,
        Others, RowVersion);

    protected override void ShowFields(UrinalysisDetails d)
    {
        ShowHeader(d.Header, d.Photo);
        Color.Value = d.Color;
        Appearance.Value = d.Appearance;
        Reaction.Value = d.Reaction;
        SPGravity.Value = d.SPGravity;
        Albumin.Value = d.Albumin;
        Sugar.Value = d.Sugar;
        PusCells.Value = d.PusCells;
        RedCells.Value = d.RedCells;
        MucusThreads.Value = d.MucusThreads;
        EpithelialCells.Value = d.EpithelialCells;
        AmorphousUratesPO4.Value = d.AmorphousUratesPO4;
        Bacteria.Value = d.Bacteria;
        Casts.Value = d.Casts;
        Crystals.Value = d.Crystals;
        Others = d.Others;
        RowVersion = d.RowVersion;
    }

    protected override void ResetDetail() => Others = null;

    protected override IEnumerable<DefaultField> ExtraDefaultFields() => [new("Others", () => Others, v => Others = v)];

    protected override Task<Result<PrintableReport>> GetPrintableAsync(long id) =>
        Call<IUrinalysisService, Result<PrintableReport>>(s => s.GetPrintableAsync(id));

    [RelayCommand(CanExecute = nameof(CanEditLists))]
    private Task EditOthersTemplatesAsync() => EditMultiLineAsync(EntryFields.UrinalysisOthers);
}