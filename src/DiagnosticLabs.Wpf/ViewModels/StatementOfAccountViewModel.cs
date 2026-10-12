using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Application.Lookups;
using DiagnosticLabs.Application.Reports;
using DiagnosticLabs.Domain.Billing;
using DiagnosticLabs.Wpf.Printing;
using DiagnosticLabs.Wpf.Services;
using Microsoft.Extensions.Logging;

namespace DiagnosticLabs.Wpf.ViewModels;

/// <summary>One registration of the statement being prepared, as the grid shows it.</summary>
public sealed class StatementLineItem(StatementLine line)
{
    public string PatientName { get; } = line.PatientName;

    public string RegistrationCode { get; } = line.RegistrationCode;

    public string DateText { get; } = line.Date.ToString("MMM d, yyyy", CultureInfo.InvariantCulture);

    public string Gross { get; } = StatementListRow.Money(line.Gross);

    public string Discount { get; } = StatementListRow.Money(line.Discount);

    public string Net { get; } = StatementListRow.Money(line.Net);

    public string Paid { get; } = StatementListRow.Money(line.Paid);

    public string Balance { get; } = StatementListRow.Money(line.Balance);
}

/// <summary>One saved statement in the list.</summary>
public sealed class StatementListRow(StatementListItem item)
{
    public long Id { get; } = item.Id;

    public string Number { get; } = item.Number;

    public bool IsVoid { get; } = item.IsVoid;

    public string TypeText { get; } = item.Mode == StatementMode.Company ? "Company" : "Patient";

    /// <summary>The company (with its batch) or the patient.</summary>
    public string Name { get; } = item.Batch is { Length: > 0 } batch ? $"{item.Name} - {batch}" : item.Name;

    public string Period { get; } = (item.From, item.To) switch
    {
        ({ } from, { } to) => $"{Day(from)} to {Day(to)}",
        ({ } from, null) => $"from {Day(from)}",
        (null, { } to) => $"up to {Day(to)}",
        _ => "all dates",
    };

    public string GeneratedText { get; } = Day(item.GeneratedOn);

    public string Lines { get; } = item.LineCount.ToString(CultureInfo.InvariantCulture);

    public string Net { get; } = Money(item.Net);

    public string Balance { get; } = item.Mode == StatementMode.Patient ? Money(item.Balance) : string.Empty;

    public string StatusText { get; } = item.IsVoid ? "VOID" : string.Empty;

    private static string Day(DateOnly day) => day.ToString("MMM d, yyyy", CultureInfo.InvariantCulture);

    public static string Money(decimal amount) => amount.ToString("N2", CultureInfo.InvariantCulture);
}

public sealed record StatementModeFilter(StatementMode? Value, string Name);

/// <summary>
/// Statements of Account. The Generate tab prepares one: for a company (the registrations charged to it, optionally one batch and a range of days) or
/// for a patient (everything billed to them, optionally in a range of days); Preview lists what would go on it and Generate saves it with a number and
/// prints it. The Saved tab finds the statements already made to reprint, void or regenerate them (a regenerated statement is a new one made from
/// the registrations as they are now; the old one is voided).
/// </summary>
public partial class StatementOfAccountViewModel(
    IServiceRunner runner,
    ICurrentUser user,
    IReportPreviewDialog preview,
    IDialogService dialogs,
    ILogger<StatementOfAccountViewModel> logger) : ViewModelBase(logger)
{
    public const string AnyBatch = "(all batches)";

    private int _suggestVersion;
    private bool _settingPatient;

    /// <summary>0 = Generate, 1 = Saved statements.</summary>
    [ObservableProperty]
    private int _selectedTab;

    // ---------------------------------------------------------------- the Generate tab

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPatientMode))]
    private bool _isCompanyMode = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanPickBatch))]
    private long _companyId;

    [ObservableProperty]
    private string _batch = AnyBatch;

    [ObservableProperty]
    private string _attention = string.Empty;

    [ObservableProperty]
    private string _patientText = string.Empty;

    [ObservableProperty]
    private long _patientId;

    [ObservableProperty]
    private DateTime? _dateFrom;

    [ObservableProperty]
    private DateTime? _dateTo;

    [ObservableProperty]
    private string _summary = string.Empty;

    public bool IsPatientMode
    {
        get => !IsCompanyMode;
        set => IsCompanyMode = !value;
    }

    public ObservableCollection<LookupOption> Companies { get; } = [];

    public ObservableCollection<string> Batches { get; } = [AnyBatch];

    public ObservableCollection<StatementPatientSuggestion> Suggestions { get; } = [];

    /// <summary>What a statement made now would contain.</summary>
    public ObservableCollection<StatementLineItem> Lines { get; } = [];

    public bool HasSuggestions => Suggestions.Count > 0;

    public bool HasLines => Lines.Count > 0;

    /// <summary>A batch belongs to a company, so the batches are offered once a company is picked.</summary>
    public bool CanPickBatch => CompanyId != 0;

    public bool CanGenerate => user.Can(ModuleIds.StatementOfAccounts, ModuleAction.Create);

    public bool CanReprint => user.Can(ModuleIds.StatementOfAccounts, ModuleAction.Print);

    public bool CanVoid => user.Can(ModuleIds.StatementOfAccounts, ModuleAction.Delete);

    partial void OnIsCompanyModeChanged(bool value) => ClearPreview();

    partial void OnCompanyIdChanged(long value)
    {
        ClearPreview();
        _ = LoadBatchesAsync(value);
    }

    partial void OnBatchChanged(string value) => ClearPreview();

    partial void OnAttentionChanged(string value) => ClearPreview();

    partial void OnDateFromChanged(DateTime? value) => ClearPreview();

    partial void OnDateToChanged(DateTime? value) => ClearPreview();

    partial void OnPatientIdChanged(long value) => ClearPreview();

    partial void OnPatientTextChanged(string value)
    {
        if (_settingPatient)
            return;

        // Typing again after a patient was picked starts the search over.
        PatientId = 0;
        _ = SuggestAsync(value);
    }

    /// <summary>The preview belongs to the parameters it was made with.</summary>
    private void ClearPreview()
    {
        ClearMessage();
        Summary = string.Empty;
        if (Lines.Count == 0)
            return;

        Lines.Clear();
        LinesChanged();
    }

    private void LinesChanged()
    {
        OnPropertyChanged(nameof(HasLines));
        GenerateCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private async Task InitializeAsync()
    {
        var companies = await runner.RunAsync<IReferenceLookups, IReadOnlyList<LookupOption>>(s => s.GetCompaniesAsync());
        Companies.Clear();
        foreach (var company in companies)
            Companies.Add(company);

        await LoadSavedAsync();
    }

    private async Task LoadBatchesAsync(long companyId)
    {
        var names = companyId == 0
            ? []
            : await runner.RunAsync<IStatementOfAccountService, IReadOnlyList<string>>(s => s.GetChargedBatchesAsync(companyId));

        // The company may have been changed again while the batches were loading.
        if (companyId != CompanyId)
            return;

        Batches.Clear();
        Batches.Add(AnyBatch);
        foreach (var name in names)
            Batches.Add(name);
        Batch = AnyBatch;
    }

    private async Task SuggestAsync(string text)
    {
        var version = ++_suggestVersion;
        if (text.Trim().Length < StatementOfAccountService.MinSuggestionLength)
        {
            ClearSuggestions();
            return;
        }

        await Task.Delay(250); // wait for a pause in typing
        if (version != _suggestVersion)
            return;

        var found = await runner.RunAsync<IStatementOfAccountService, IReadOnlyList<StatementPatientSuggestion>>(s => s.SuggestPatientsAsync(text));
        if (version != _suggestVersion || PatientId != 0)
            return;

        Suggestions.Clear();
        foreach (var suggestion in found)
            Suggestions.Add(suggestion);
        OnPropertyChanged(nameof(HasSuggestions));
    }

    private void ClearSuggestions()
    {
        _suggestVersion++;
        Suggestions.Clear();
        OnPropertyChanged(nameof(HasSuggestions));
    }

    [RelayCommand]
    private void PickPatient(StatementPatientSuggestion? patient)
    {
        if (patient is null)
            return;

        ClearSuggestions();
        _settingPatient = true;
        try
        {
            PatientText = $"{patient.PatientName} ({patient.PatientCode})";
            PatientId = patient.Id;
        }
        finally
        {
            _settingPatient = false;
        }
    }

    /// <summary>The range is optional: empty dates mean every date.</summary>
    [RelayCommand]
    private void ClearDates()
    {
        DateFrom = null;
        DateTo = null;
    }

    private StatementRequest BuildRequest() => new(
        IsCompanyMode ? StatementMode.Company : StatementMode.Patient,
        IsCompanyMode && CompanyId != 0 ? CompanyId : null,
        IsCompanyMode && Batch != AnyBatch ? Batch : null,
        !IsCompanyMode && PatientId != 0 ? PatientId : null,
        DateFrom is { } from ? DateOnly.FromDateTime(from) : null,
        DateTo is { } to ? DateOnly.FromDateTime(to) : null,
        IsCompanyMode ? Attention : null);

    [RelayCommand(CanExecute = nameof(CanGenerate))]
    private Task PreviewAsync() => RunAsync(async () =>
    {
        ClearPreview();
        var request = BuildRequest();
        var result = await runner.RunAsync<IStatementOfAccountService, Result<StatementData>>(s => s.PreviewAsync(request));
        if (result.IsFailure)
        {
            ShowError(result.Error.Message);
            return;
        }

        var statement = result.Value;
        foreach (var line in statement.Lines)
            Lines.Add(new StatementLineItem(line));
        LinesChanged();

        var count = $"{Lines.Count} registration{(Lines.Count == 1 ? string.Empty : "s")}";
        Summary = statement.Mode == StatementMode.Company
            ? $"{count}.  Gross {StatementListRow.Money(statement.GrossTotal)}   Discount {StatementListRow.Money(statement.DiscountTotal)}   Net {StatementListRow.Money(statement.NetTotal)}"
            : $"{count}.  Net {StatementListRow.Money(statement.NetTotal)}   Paid {StatementListRow.Money(statement.PaidTotal)}   Balance {StatementListRow.Money(statement.BalanceTotal)}";
    });

    /// <summary>Saves the statement (it gets its number) and shows it for printing.</summary>
    [RelayCommand(CanExecute = nameof(CanGenerateNow))]
    private Task GenerateAsync() => RunAsync(async () =>
    {
        var request = BuildRequest();
        var result = await runner.RunAsync<IStatementOfAccountService, Result<StatementData>>(s => s.GenerateAsync(request));
        if (result.IsFailure)
        {
            ShowError(result.Error.Message);
            return;
        }

        ClearPreview();
        ShowInfo($"Saved as {result.Value.Number}. Find it again under Saved statements.");
        await LoadSavedAsync();
        Show(result.Value);
    });

    private bool CanGenerateNow() => CanGenerate && Lines.Count > 0;

    private void Show(StatementData statement)
    {
        var who = statement.Mode == StatementMode.Company ? statement.CompanyName : statement.PatientName;
        preview.Show(StatementDocumentBuilder.Build(statement), "Statement of Account", $"{statement.Number} - {who}".Trim(' ', '-'));
    }

    // ---------------------------------------------------------------- the Saved tab

    public static IReadOnlyList<StatementModeFilter> ModeFilters { get; } =
    [
        new(null, "All statements"),
        new(StatementMode.Company, "Company statements"),
        new(StatementMode.Patient, "Patient statements"),
    ];

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private StatementModeFilter _modeFilter = ModeFilters[0];

    [ObservableProperty]
    private DateTime? _generatedFrom;

    [ObservableProperty]
    private DateTime? _generatedTo;

    [ObservableProperty]
    private bool _includeVoid = true;

    [ObservableProperty]
    private StatementListRow? _selected;

    [ObservableProperty]
    private string _savedMessage = string.Empty;

    public ObservableCollection<StatementListRow> Saved { get; } = [];

    partial void OnSelectedChanged(StatementListRow? value)
    {
        ReprintCommand.NotifyCanExecuteChanged();
        RegenerateCommand.NotifyCanExecuteChanged();
        VoidCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private Task SearchSavedAsync() => RunAsync(LoadSavedAsync);

    private async Task LoadSavedAsync()
    {
        var search = new StatementSearch(
            SearchText,
            ModeFilter.Value,
            GeneratedFrom is { } from ? DateOnly.FromDateTime(from) : null,
            GeneratedTo is { } to ? DateOnly.FromDateTime(to) : null,
            IncludeVoid);
        var found = await runner.RunAsync<IStatementOfAccountService, IReadOnlyList<StatementListItem>>(s => s.SearchAsync(search));

        var keep = Selected?.Id;
        Saved.Clear();
        foreach (var item in found)
            Saved.Add(new StatementListRow(item));
        Selected = Saved.FirstOrDefault(r => r.Id == keep);
        SavedMessage = Saved.Count == 0 ? "No statements found." : string.Empty;
    }

    [RelayCommand]
    private void ClearSavedDates()
    {
        GeneratedFrom = null;
        GeneratedTo = null;
    }

    private bool CanReprintNow() => CanReprint && Selected is not null;

    private bool CanRegenerateNow() => CanGenerate && CanVoid && Selected is not null;

    private bool CanVoidNow() => CanVoid && Selected is { IsVoid: false };

    /// <summary>Prints a saved statement exactly as it was generated (a voided one prints marked VOID).</summary>
    [RelayCommand(CanExecute = nameof(CanReprintNow))]
    private Task ReprintAsync() => RunAsync(async () =>
    {
        if (Selected is not { } row)
            return;

        var result = await runner.RunAsync<IStatementOfAccountService, Result<StatementData>>(s => s.GetAsync(row.Id));
        if (result.IsFailure)
        {
            SavedMessage = result.Error.Message;
            return;
        }

        SavedMessage = string.Empty;
        Show(result.Value);
    });

    [RelayCommand(CanExecute = nameof(CanVoidNow))]
    private Task VoidAsync() => RunAsync(async () =>
    {
        if (Selected is not { } row)
            return;

        if (!dialogs.Confirm($"Void {row.Number}? It stays in the list and prints marked VOID, but it no longer counts as a statement that was given.", "Void statement"))
            return;

        var result = await runner.RunAsync<IStatementOfAccountService, Result>(s => s.VoidAsync(row.Id));
        await LoadSavedAsync();
        if (result.IsFailure)
            SavedMessage = result.Error.Message;
        else
            SavedMessage = $"{row.Number} is void.";
    });

    [RelayCommand(CanExecute = nameof(CanRegenerateNow))]
    private Task RegenerateAsync() => RunAsync(async () =>
    {
        if (Selected is not { } row)
            return;

        var warning = row.IsVoid
            ? $"Make a new statement from the registrations as they are now, with the same parameters as {row.Number}?"
            : $"Make a new statement from the registrations as they are now, with the same parameters as {row.Number}? {row.Number} will be voided.";
        if (!dialogs.Confirm(warning, "Regenerate statement"))
            return;

        var result = await runner.RunAsync<IStatementOfAccountService, Result<StatementData>>(s => s.RegenerateAsync(row.Id));
        if (result.IsFailure)
        {
            SavedMessage = result.Error.Message;
            return;
        }

        await LoadSavedAsync();
        SavedMessage = row.IsVoid ? $"Saved as {result.Value.Number}." : $"Saved as {result.Value.Number}; {row.Number} is void.";
        Show(result.Value);
    });
}
