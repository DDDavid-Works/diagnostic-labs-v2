using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiagnosticLabs.Application.Stations;
using DiagnosticLabs.Wpf.Services;
using Microsoft.Extensions.Logging;

namespace DiagnosticLabs.Wpf.ViewModels;

/// <summary>One line of the Stations list, ready to show.</summary>
public sealed record StationLine(string Station, string Version, string Status, string LastUser, string LastSeen, bool IsBehind);

/// <summary>
/// Administration screen: every PC that has run the app, the release it was on when last seen, and whether that is the latest release on the
/// server's update share. A PC that is behind picks the release up the next time its user opens the app from the shortcut.
/// </summary>
public partial class StationsViewModel(IServiceRunner runner, IUpdateChecker updates, ILogger<StationsViewModel> logger) : ViewModelBase(logger)
{
    public ObservableCollection<StationLine> Stations { get; } = [];

    [ObservableProperty]
    private string _summary = string.Empty;

    [RelayCommand]
    private Task RefreshAsync() => RunAsync(async () =>
    {
        ClearMessage();
        var latest = await updates.GetShareVersionAsync();
        var result = await runner.RunAsync<IStationVersionService, DiagnosticLabs.Application.Common.Result<IReadOnlyList<StationRow>>>(s => s.GetStationsAsync(latest));
        if (result.IsFailure)
        {
            ShowError(result.Error.Message);
            return;
        }

        Stations.Clear();
        foreach (var row in result.Value)
        {
            Stations.Add(new StationLine(
                row.StationName,
                row.AppVersion,
                row.State switch
                {
                    StationState.UpToDate => "Up to date",
                    StationState.Behind => "Behind",
                    _ => "Unknown",
                },
                row.LastUserName ?? string.Empty,
                row.IsOpenNow ? "Open now" : row.LastSeenUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
                row.State == StationState.Behind));
        }

        var behind = Stations.Count(s => s.IsBehind);
        var newest = latest ?? result.Value.Select(r => r.AppVersion).Aggregate((string?)null, (best, v) => best is null || StationVersionService.Compare(v, best) > 0 ? v : best);
        Summary = Stations.Count == 0
            ? "No station has reported yet. A PC appears here after someone signs in on it."
            : behind == 0
                ? $"All {Stations.Count} stations are on version {newest}."
                : $"{behind} of {Stations.Count} stations are not on version {newest} yet; they update when their users open the app from the shortcut.";
        if (latest is null && Stations.Count > 0)
            Summary += " (The server's update share could not be read, so the newest version any station reported is used.)";
    });
}
