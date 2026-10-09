using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Application.Entries;
using DiagnosticLabs.Wpf.Services;
using Microsoft.Extensions.Logging;

namespace DiagnosticLabs.Wpf.ViewModels;

/// <summary>Shared by both builders: the heading, the scope line and closing the dialog.</summary>
public abstract partial class EntryBuilderViewModel(ILogger logger) : ViewModelBase(logger)
{
    [ObservableProperty]
    private string _fieldName = string.Empty;

    [ObservableProperty]
    private string _scopeName = string.Empty;

    /// <summary>Raised with <c>true</c> when the list was saved, <c>false</c> when cancelled.</summary>
    public event EventHandler<bool>? CloseRequested;

    public abstract IAsyncRelayCommand InitializeCommand { get; }

    protected void Close(bool saved) => CloseRequested?.Invoke(this, saved);

    [RelayCommand]
    private void Cancel() => Close(false);
}

// ------------------------------------------------------------------ single line

public partial class SingleLineRowViewModel : ObservableObject
{
    public long Id { get; init; }

    [ObservableProperty]
    private string _value = string.Empty;

    /// <summary>Set on a freshly added row so its box takes the cursor once it appears.</summary>
    public bool FocusRequested { get; set; }
}

public partial class SingleLineEntryBuilderViewModel(
    IServiceRunner runner, EntryField field, int hostModuleId, ILogger<SingleLineEntryBuilderViewModel> logger)
    : EntryBuilderViewModel(logger)
{
    public ObservableCollection<SingleLineRowViewModel> Rows { get; } = [];

    public override IAsyncRelayCommand InitializeCommand => LoadCommand;

    [RelayCommand]
    private Task LoadAsync() => RunAsync(async () =>
    {
        var result = await runner.RunAsync<IEntryService, Result<SingleLineEntryList>>(s => s.GetSingleLineAsync(field, hostModuleId));
        if (result.IsFailure)
        {
            ShowError(result.Error.Message);
            return;
        }

        Show(result.Value);
    });

    [RelayCommand]
    private void Add() => Rows.Add(new SingleLineRowViewModel { FocusRequested = true });

    [RelayCommand]
    private void Remove(SingleLineRowViewModel? row)
    {
        if (row is not null)
            Rows.Remove(row);
    }

    [RelayCommand]
    private Task SaveAsync() => RunAsync(async () =>
    {
        // A row that was added but never typed in is simply dropped.
        var items = Rows
            .Where(r => r.Id != 0 || !string.IsNullOrWhiteSpace(r.Value))
            .Select(r => new SingleLineEntry(r.Id, r.Value))
            .ToList();

        var result = await runner.RunAsync<IEntryService, Result<SingleLineEntryList>>(s => s.SaveSingleLineAsync(field, hostModuleId, items));
        if (result.IsFailure)
        {
            ShowError(result.Error.Message);
            return;
        }

        Close(true);
    });

    private void Show(SingleLineEntryList list)
    {
        FieldName = list.FieldName;
        ScopeName = list.ScopeName;
        Rows.Clear();
        foreach (var item in list.Items)
            Rows.Add(new SingleLineRowViewModel { Id = item.Id, Value = item.Value });
    }
}

// ------------------------------------------------------------------ multi line

public partial class MultiLineRowViewModel : ObservableObject
{
    public long Id { get; init; }

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _text = string.Empty;
}

public partial class MultiLineEntryBuilderViewModel(
    IServiceRunner runner, EntryField field, int hostModuleId, ILogger<MultiLineEntryBuilderViewModel> logger)
    : EntryBuilderViewModel(logger)
{
    public ObservableCollection<MultiLineRowViewModel> Rows { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private MultiLineRowViewModel? _selectedRow;

    public bool HasSelection => SelectedRow is not null;

    public override IAsyncRelayCommand InitializeCommand => LoadCommand;

    [RelayCommand]
    private Task LoadAsync() => RunAsync(async () =>
    {
        var result = await runner.RunAsync<IEntryService, Result<MultiLineEntryList>>(s => s.GetMultiLineAsync(field, hostModuleId));
        if (result.IsFailure)
        {
            ShowError(result.Error.Message);
            return;
        }

        FieldName = result.Value.FieldName;
        ScopeName = result.Value.ScopeName;
        Rows.Clear();
        foreach (var item in result.Value.Items)
            Rows.Add(new MultiLineRowViewModel { Id = item.Id, Title = item.Title, Text = item.Text });
        SelectedRow = Rows.FirstOrDefault();
    });

    [RelayCommand]
    private void Add()
    {
        var row = new MultiLineRowViewModel();
        Rows.Add(row);
        SelectedRow = row;
    }

    [RelayCommand]
    private void Remove()
    {
        if (SelectedRow is not { } row)
            return;

        var index = Rows.IndexOf(row);
        Rows.Remove(row);
        SelectedRow = Rows.Count == 0 ? null : Rows[Math.Min(index, Rows.Count - 1)];
    }

    [RelayCommand]
    private Task SaveAsync() => RunAsync(async () =>
    {
        // An added row with no name and no text is simply dropped.
        var items = Rows
            .Where(r => r.Id != 0 || !string.IsNullOrWhiteSpace(r.Title) || !string.IsNullOrWhiteSpace(r.Text))
            .Select(r => new MultiLineEntry(r.Id, r.Title, r.Text))
            .ToList();

        var result = await runner.RunAsync<IEntryService, Result<MultiLineEntryList>>(s => s.SaveMultiLineAsync(field, hostModuleId, items));
        if (result.IsFailure)
        {
            ShowError(result.Error.Message);
            return;
        }

        Close(true);
    });
}
