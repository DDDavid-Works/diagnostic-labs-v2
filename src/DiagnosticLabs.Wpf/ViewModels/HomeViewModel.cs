using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Board;
using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Application.Lookups;
using DiagnosticLabs.Wpf.Services;
using Microsoft.Extensions.Logging;

namespace DiagnosticLabs.Wpf.ViewModels;

/// <summary>One result form of a registration on the home screen: whether its result has been made, and a click that opens it.</summary>
public sealed class HomeChip(BoardService service, long registrationId, bool canUse)
{
    public string Name { get; } = service.Name;

    public bool IsMade { get; } = service.ResultCount > 0;

    public int Count { get; } = service.ResultCount;

    public int? ModuleId { get; } = service.ModuleId;

    public long RegistrationId { get; } = registrationId;

    public long? LatestResultId { get; } = service.LatestResultId;

    /// <summary>A form that is not built yet, or one the user may not use, is shown but cannot be opened.</summary>
    public bool CanOpen { get; } = canUse && service.CanOpen;

    public string Caption => Count > 1 ? $"{Name}  x{Count}" : Name;

    public string ToolTip => !service.CanOpen
        ? (service.ModuleId is null ? "No result form for this service" : "This form is not available in the new app yet")
        : !CanOpen ? "You do not have access to this form"
        : IsMade ? "Open the result" + (Count > 1 ? " (the latest of " + Count + ")" : string.Empty) : "Make the result";
}

/// <summary>One registration on the home screen.</summary>
public sealed class HomeRow(BoardItem item, IReadOnlyList<HomeChip> chips, bool canPay)
{
    public long RegistrationId { get; } = item.RegistrationId;

    public string PatientName { get; } = item.PatientName;

    public string Code { get; } = item.RegistrationCode;

    public string DateText { get; } = item.Date.ToString("dd MMM yyyy");

    public string Details { get; } = string.Join("  -  ", new[] { item.CompanyName, string.IsNullOrWhiteSpace(item.BatchName) ? null : item.BatchName }.Where(s => !string.IsNullOrWhiteSpace(s)));

    public IReadOnlyList<HomeChip> Chips { get; } = chips;

    public bool CanPay { get; } = canPay;

    public PaymentState PaymentState { get; } = item.Payment.State;

    public string PaymentText { get; } = item.Payment.State switch
    {
        PaymentState.Paid => "Paid",
        PaymentState.Charged => "Charged to company",
        PaymentState.Partial => $"Partial: {item.Payment.Paid:N2} of {item.Payment.Due:N2}",
        _ => $"Unpaid: {item.Payment.Due:N2}",
    };
}

/// <summary>
/// The first screen after signing in: today's registrations (or any day, company, name or status), each with its payment state and the
/// result forms its services call for, so the next step of a visit is one click away. Paged, newest first.
/// </summary>
public partial class HomeViewModel(
    IServiceRunner runner,
    ICurrentUser user,
    INavigationService navigation,
    ILogger<HomeViewModel> logger) : ViewModelBase(logger)
{
    private static readonly LookupOption AnyCompany = new(0, "(all companies)");

    private int _searchVersion;
    private bool _loadingFilters;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private long _companyId;

    [ObservableProperty]
    private DateTime? _dateFrom = DateTime.Today;

    [ObservableProperty]
    private DateTime? _dateTo = DateTime.Today;

    [ObservableProperty]
    private StatusChoice _status = StatusChoices[0];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageDisplay), nameof(CanGoPrevious), nameof(CanGoNext))]
    private int _page = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageDisplay), nameof(CanGoNext))]
    private int _totalPages = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageDisplay), nameof(HasNoRows))]
    private int _totalCount;

    [ObservableProperty]
    private int _pageSize = 20;

    public static IReadOnlyList<StatusChoice> StatusChoices { get; } =
    [
        new(BoardStatus.All, "All registrations"),
        new(BoardStatus.Unpaid, "Unpaid"),
        new(BoardStatus.ResultsPending, "Results pending"),
    ];

    public IReadOnlyList<int> PageSizes { get; } = [10, 20, 50, 100];

    public ObservableCollection<LookupOption> Companies { get; } = [];

    public ObservableCollection<HomeRow> Rows { get; } = [];

    public string PageDisplay => $"Page {Page} of {TotalPages} ({TotalCount} registration{(TotalCount == 1 ? string.Empty : "s")})";

    public bool CanGoPrevious => Page > 1;

    public bool CanGoNext => Page < TotalPages;

    public bool HasNoRows => TotalCount == 0;

    public string EmptyText => DateFrom == DateTime.Today && DateTo == DateTime.Today && string.IsNullOrWhiteSpace(SearchText) && CompanyId == 0 && Status.Value == BoardStatus.All
        ? "No registrations yet today."
        : "No registration matches. Widen the dates or clear the filters.";

    // Typing in the search box searches after a short pause; the other filters search at once.
    partial void OnSearchTextChanged(string value)
    {
        if (_loadingFilters)
            return;

        var version = ++_searchVersion;
        _ = DebouncedSearchAsync(version);
    }

    private async Task DebouncedSearchAsync(int version)
    {
        await Task.Delay(350).ConfigureAwait(false);
        if (version != _searchVersion)
            return;

        // The list belongs to the screen, so the search always runs on the UI thread.
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null)
            await SearchAsync();
        else
            await dispatcher.InvokeAsync(() => SearchAsync()).Task.Unwrap();
    }

    partial void OnCompanyIdChanged(long value) => SearchNow();

    partial void OnDateFromChanged(DateTime? value) => SearchNow();

    partial void OnDateToChanged(DateTime? value) => SearchNow();

    partial void OnStatusChanged(StatusChoice value) => SearchNow();

    partial void OnPageSizeChanged(int value) => SearchNow();

    private void SearchNow()
    {
        if (_loadingFilters)
            return;

        _searchVersion++;
        _ = SearchAsync();
    }

    [RelayCommand]
    private async Task InitializeAsync()
    {
        var companies = await runner.RunAsync<IReferenceLookups, IReadOnlyList<LookupOption>>(s => s.GetCompaniesAsync());
        Companies.Clear();
        Companies.Add(AnyCompany);
        foreach (var company in companies)
            Companies.Add(company);

        await SearchAsync();
    }

    /// <summary>Searches again from the first page (also used to refresh after a result or a payment was saved elsewhere).</summary>
    [RelayCommand]
    private Task SearchAsync() => RunAsync(() =>
    {
        Page = 1;
        return LoadPageAsync();
    });

    [RelayCommand]
    private Task RefreshAsync() => RunAsync(LoadPageAsync);

    [RelayCommand]
    private Task NextPageAsync() => RunAsync(() =>
    {
        if (CanGoNext)
            Page++;
        return LoadPageAsync();
    });

    [RelayCommand]
    private Task PreviousPageAsync() => RunAsync(() =>
    {
        if (CanGoPrevious)
            Page--;
        return LoadPageAsync();
    });

    /// <summary>Back to today's registrations with no other filter.</summary>
    [RelayCommand]
    private void Today()
    {
        _loadingFilters = true;
        try
        {
            SearchText = string.Empty;
            CompanyId = 0;
            DateFrom = DateTime.Today;
            DateTo = DateTime.Today;
            Status = StatusChoices[0];
        }
        finally
        {
            _loadingFilters = false;
        }

        SearchNow();
    }

    /// <summary>Every day (no date limit).</summary>
    [RelayCommand]
    private void AllDays()
    {
        _loadingFilters = true;
        try
        {
            DateFrom = null;
            DateTo = null;
        }
        finally
        {
            _loadingFilters = false;
        }

        SearchNow();
    }

    [RelayCommand]
    private void OpenChip(HomeChip? chip)
    {
        if (chip is { CanOpen: true, ModuleId: { } moduleId })
            navigation.OpenResult(moduleId, chip.RegistrationId, chip.IsMade ? chip.LatestResultId : null);
    }

    [RelayCommand]
    private void Pay(HomeRow? row)
    {
        if (row is { CanPay: true })
            navigation.OpenPayment(row.RegistrationId);
    }

    private async Task LoadPageAsync()
    {
        var search = new BoardSearch(
            SearchText,
            CompanyId == 0 ? null : CompanyId,
            DateFrom is { } from ? DateOnly.FromDateTime(from) : null,
            DateTo is { } to ? DateOnly.FromDateTime(to) : null,
            Status.Value,
            Page,
            PageSize);

        var result = await runner.RunAsync<IRegistrationBoardService, Result<PagedResult<BoardItem>>>(s => s.SearchAsync(search));
        if (result.IsFailure)
        {
            ShowError(result.Error.Message);
            return;
        }

        var paged = result.Value;
        if (paged.Page > paged.TotalPages)
        {
            // e.g. the last page emptied after a refresh
            Page = paged.TotalPages;
            await LoadPageAsync();
            return;
        }

        ClearMessage();
        Rows.Clear();
        var canPay = user.Can(ModuleIds.Payments, ModuleAction.Create) || user.Can(ModuleIds.Payments, ModuleAction.Edit);
        foreach (var item in paged.Items)
        {
            var chips = item.Services.Select(s => new HomeChip(s, item.RegistrationId, CanUse(s))).ToList();
            Rows.Add(new HomeRow(item, chips, canPay));
        }

        TotalCount = paged.TotalCount;
        TotalPages = paged.TotalPages;
        Page = paged.Page;
        OnPropertyChanged(nameof(EmptyText));
    }

    // Starting a result needs "create" in that form; opening one that was made needs only access to it.
    private bool CanUse(BoardService service) =>
        service.ModuleId is { } id && (service.ResultCount > 0 ? user.CanAccess(id) : user.Can(id, ModuleAction.Create));
}

public sealed record StatusChoice(BoardStatus Value, string Name);
