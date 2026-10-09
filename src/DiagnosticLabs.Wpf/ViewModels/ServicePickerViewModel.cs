using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiagnosticLabs.Application.Lookups;

namespace DiagnosticLabs.Wpf.ViewModels;

/// <summary>One service as a tile in the picker.</summary>
public partial class ServiceTileViewModel(ServiceOption option, bool isSelected) : ObservableObject
{
    public long Id { get; } = option.Id;

    public string Name { get; } = option.Name;

    public decimal Price { get; } = option.Price;

    [ObservableProperty]
    private bool _isSelected = isSelected;
}

/// <summary>
/// The point-of-sale style service chooser: every service is a tile, the ones already on the list are ticked,
/// and unticking one removes it. Used wherever a table of services is built (packages, registrations).
/// </summary>
public partial class ServicePickerViewModel : ObservableObject
{
    public ServicePickerViewModel(IReadOnlyList<ServiceOption> options, IEnumerable<long> selectedIds)
    {
        var selected = selectedIds.ToHashSet();
        foreach (var option in options)
        {
            var tile = new ServiceTileViewModel(option, selected.Contains(option.Id));
            tile.PropertyChanged += OnTileChanged;
            Tiles.Add(tile);
            VisibleTiles.Add(tile);
        }
    }

    /// <summary>Every service, in the order the lab uses; the selection lives here whatever the search shows.</summary>
    public ObservableCollection<ServiceTileViewModel> Tiles { get; } = [];

    /// <summary>The tiles that match the search box.</summary>
    public ObservableCollection<ServiceTileViewModel> VisibleTiles { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NothingMatches))]
    private string _searchText = string.Empty;

    public bool NothingMatches => VisibleTiles.Count == 0;

    public int SelectedCount => Tiles.Count(t => t.IsSelected);

    public decimal SelectedTotal => Tiles.Where(t => t.IsSelected).Sum(t => t.Price);

    public string Summary => SelectedCount == 0 ? "Nothing selected" : $"{SelectedCount} selected - {SelectedTotal:N2}";

    public IReadOnlyList<long> SelectedIds => [.. Tiles.Where(t => t.IsSelected).Select(t => t.Id)];

    /// <summary>Raised with <c>true</c> for OK and <c>false</c> for Cancel.</summary>
    public event EventHandler<bool>? CloseRequested;

    partial void OnSearchTextChanged(string value)
    {
        var words = value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        VisibleTiles.Clear();
        foreach (var tile in Tiles.Where(t => words.All(w => t.Name.Contains(w, StringComparison.OrdinalIgnoreCase))))
            VisibleTiles.Add(tile);

        OnPropertyChanged(nameof(NothingMatches));
    }

    private void OnTileChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ServiceTileViewModel.IsSelected))
            return;

        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(SelectedTotal));
        OnPropertyChanged(nameof(Summary));
    }

    [RelayCommand]
    private void ClearSelection()
    {
        foreach (var tile in Tiles)
            tile.IsSelected = false;
    }

    [RelayCommand]
    private void ClearSearch() => SearchText = string.Empty;

    [RelayCommand]
    private void Ok() => CloseRequested?.Invoke(this, true);

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(this, false);
}
