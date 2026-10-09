using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Wpf.Services;
using Microsoft.Extensions.Logging;

namespace DiagnosticLabs.Wpf.ViewModels;

/// <summary>What the shared list-and-editor layout needs from a screen's view model.</summary>
public interface ICrudScreen
{
    IAsyncRelayCommand InitializeCommand { get; }

    ICommand SearchCommand { get; }

    ICommand ChangePageSizeCommand { get; }
}

/// <summary>
/// The whole maintenance-screen workflow: search with paging on the left, one record being edited on the right.
/// A concrete screen only declares its fields and how they map to the service's input and details.
/// </summary>
public abstract partial class CrudViewModel<TService, TListItem, TDetails, TInput>(
    IServiceRunner runner,
    IDialogService dialogs,
    ICurrentUser currentUser,
    int moduleId,
    string title,
    string entityName,
    bool hasActiveFlag,
    ILogger logger) : ViewModelBase(logger), ICrudScreen
    where TService : ICrudService<TListItem, TDetails, TInput>
    where TListItem : class, IHasId
    where TDetails : IHasId
    where TInput : ICrudInput
{
    protected ICurrentUser CurrentUser { get; } = currentUser;

    public string Title { get; } = title;

    /// <summary>Whether rows of this list can be switched off (reference lists) and so show the Active controls.</summary>
    public bool HasActiveFlag { get; } = hasActiveFlag;

    private bool _suppressOpen;

    public ObservableCollection<TListItem> Items { get; } = [];

    public IReadOnlyList<int> PageSizes { get; } = [10, 20, 50, 100];

    // ----- search & paging -----
    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _includeInactive;

    /// <summary>Whether the list of records is shown on the right. Hidden by default, like the old search popup.</summary>
    [ObservableProperty]
    private bool _isSearchVisible;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageDisplay), nameof(CanGoPrevious), nameof(CanGoNext))]
    private int _page = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageDisplay), nameof(CanGoNext))]
    private int _totalPages = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageDisplay))]
    private int _totalCount;

    [ObservableProperty]
    private int _pageSize = Paging.DefaultPageSize;

    [ObservableProperty]
    private TListItem? _selectedItem;

    public string PageDisplay => $"Page {Page} of {TotalPages} ({TotalCount} total)";

    public bool CanGoPrevious => Page > 1;

    public bool CanGoNext => Page < TotalPages;

    // ----- the record being edited -----
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNew), nameof(CanSave), nameof(CanDelete))]
    private long _id;

    [ObservableProperty]
    private bool _isActive = true;

    protected byte[] RowVersion { get; set; } = [];

    public bool IsNew => Id == 0;

    public bool CanCreate => CurrentUser.Can(moduleId, ModuleAction.Create);

    /// <summary>True for records that must not be changed from the UI (e.g. system rows).</summary>
    protected virtual bool IsLocked => false;

    public bool CanSave => !IsLocked && (IsNew ? CanCreate : CurrentUser.Can(moduleId, ModuleAction.Edit));

    public bool CanDelete => !IsNew && !IsLocked && CurrentUser.Can(moduleId, ModuleAction.Delete);

    // ----- what a concrete screen provides -----
    protected abstract TInput BuildInput();

    /// <summary>Copies a loaded record into the form fields (and child lines).</summary>
    protected abstract void ShowFields(TDetails details);

    /// <summary>Clears the form for a new record.</summary>
    protected abstract void ResetFields();

    /// <summary>Name shown in the delete confirmation and used to find a just-created record in the list.</summary>
    protected abstract string CurrentName { get; }

    /// <summary>Load dropdown lists etc. before the first search.</summary>
    protected virtual Task OnInitializeAsync() => Task.CompletedTask;

    /// <summary>Called after a successful save (e.g. refresh dropdowns that may have gained a value).</summary>
    protected virtual Task OnSavedAsync() => Task.CompletedTask;

    protected virtual string SearchTextAfterCreate => CurrentName;

    /// <summary>Called after the open record changed (a record was loaded or a new one started); raise change notices for derived state here.</summary>
    protected virtual void OnRecordChanged()
    {
    }

    /// <summary>The message shown after a successful save; screens override it to add guidance.</summary>
    protected virtual string SavedMessage(TDetails saved, bool wasNew) => "Saved successfully.";

    /// <summary>Reloads the open record and the list (e.g. after an action that changed it outside the form).</summary>
    protected async Task ReloadAsync()
    {
        await OpenAsync(Id);
        await LoadPageAsync();
    }

    /// <summary>Runs a service call in its own scope; screens use this for their dropdown lookups too.</summary>
    protected Task<TResult> Call<TOther, TResult>(Func<TOther, Task<TResult>> action) where TOther : notnull =>
        runner.RunAsync(action);

    partial void OnSelectedItemChanged(TListItem? value)
    {
        if (_suppressOpen || value is null || value.Id == Id)
            return;

        _ = RunAsync(() => OpenAsync(value.Id));
    }

    // ----- commands -----
    [RelayCommand]
    private Task InitializeAsync() => RunAsync(async () =>
    {
        await OnInitializeAsync();
        await LoadPageAsync();
        StartNew();
    });

    [RelayCommand]
    private Task SearchAsync() => RunAsync(() =>
    {
        Page = 1;
        return LoadPageAsync();
    });

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

    [RelayCommand]
    private Task ChangePageSizeAsync() => RunAsync(() =>
    {
        Page = 1;
        return LoadPageAsync();
    });

    [RelayCommand]
    private void ToggleSearch() => IsSearchVisible = !IsSearchVisible;

    [RelayCommand]
    private void NewRecord()
    {
        StartNew();
        SelectInList(null);
        ClearMessage();
    }

    [RelayCommand]
    private Task SaveAsync() => RunAsync(async () =>
    {
        var wasNew = IsNew;
        var input = BuildInput();

        var result = await runner.RunAsync<TService, Result<TDetails>>(s => s.SaveAsync(input));
        if (result.IsFailure)
        {
            ShowError(result.Error.Message);
            return;
        }

        Show(result.Value);
        ShowInfo(SavedMessage(result.Value, wasNew));

        // A new record may not match the current search; narrow the list to it so the user sees the result.
        if (wasNew)
        {
            SearchText = SearchTextAfterCreate;
            Page = 1;
        }

        await OnSavedAsync();
        await LoadPageAsync();
        SelectInList(Id);
    });

    [RelayCommand]
    private Task DeleteAsync() => RunAsync(async () =>
    {
        if (IsNew || !dialogs.Confirm($"Are you sure you want to delete {CurrentName}?", $"Delete {entityName.ToLowerInvariant()}"))
            return;

        var id = Id;
        var result = await runner.RunAsync<TService, Result>(s => s.DeleteAsync(id));
        if (result.IsFailure)
        {
            ShowError(result.Error.Message);
            return;
        }

        StartNew();
        await LoadPageAsync();
        ShowInfo("Deleted successfully.");
    });

    // ----- helpers -----
    private async Task OpenAsync(long id)
    {
        var result = await runner.RunAsync<TService, Result<TDetails>>(s => s.GetAsync(id));
        if (result.IsFailure)
        {
            ShowError(result.Error.Message);
            return;
        }

        Show(result.Value);
        ClearMessage();
    }

    private async Task LoadPageAsync()
    {
        var search = new CrudSearch(SearchText, Page, PageSize, IncludeInactive);
        var result = await runner.RunAsync<TService, Result<PagedResult<TListItem>>>(s => s.SearchAsync(search));
        if (result.IsFailure)
        {
            ShowError(result.Error.Message);
            return;
        }

        var paged = result.Value;
        if (paged.Page > paged.TotalPages)
        {
            // e.g. the last row on the last page was just deleted
            Page = paged.TotalPages;
            await LoadPageAsync();
            return;
        }

        _suppressOpen = true;
        try
        {
            Items.Clear();
            foreach (var item in paged.Items)
                Items.Add(item);
        }
        finally
        {
            _suppressOpen = false;
        }

        TotalCount = paged.TotalCount;
        TotalPages = paged.TotalPages;
        Page = paged.Page;
    }

    private void SelectInList(long? id)
    {
        _suppressOpen = true;
        try
        {
            SelectedItem = id is null ? null : Items.FirstOrDefault(i => i.Id == id);
        }
        finally
        {
            _suppressOpen = false;
        }
    }

    private void StartNew()
    {
        Id = 0;
        IsActive = true;
        RowVersion = [];
        ResetFields();
        OnPropertyChanged(nameof(CanSave));
        OnRecordChanged();
    }

    private void Show(TDetails details)
    {
        Id = details.Id;
        ShowFields(details);
        OnPropertyChanged(nameof(CanSave));
        OnRecordChanged();
    }

    IAsyncRelayCommand ICrudScreen.InitializeCommand => InitializeCommand;

    ICommand ICrudScreen.SearchCommand => SearchCommand;

    ICommand ICrudScreen.ChangePageSizeCommand => ChangePageSizeCommand;
}
