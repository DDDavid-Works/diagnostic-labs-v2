using System.Windows;
using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Stations;
using DiagnosticLabs.Domain.Settings;
using DiagnosticLabs.Wpf.Services;
using DiagnosticLabs.Wpf.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace UiHarness;

internal sealed class FakeAppInfo : IAppInfo
{
    public string Version => "1.0.0";

    public string StationName => "HARNESS-PC";
}

// Stands in for the server's update share and the launcher: the check chooses which release "is on the share".
internal sealed class FakeUpdates : IUpdateChecker
{
    public static string? ShareVersion { get; set; }

    public static int Restarts { get; set; }

    public bool CanRestart => true;

    public Task<string?> GetShareVersionAsync(CancellationToken cancellationToken = default) => Task.FromResult(ShareVersion);

    public void StartRestart() => Restarts++;
}

internal static partial class Program
{
    // The "new version ready" notice and the Stations list. The share holds 1.0.1 (set before the window opens); this harness PC runs 1.0.0.
    private static void StationsChecks(IServiceProvider provider)
    {
        _main.WindowState = WindowState.Normal;
        _main.Width = 1366;
        _main.Height = 728;
        _main.Left = 0;
        _main.Top = 0;
        Pump(800);

        Check("signing in recorded this PC", Stored(provider).Any(s => s.StationName == "HARNESS-PC" && s.AppVersion == "1.0.0"), string.Join(", ", Stored(provider).Select(s => s.StationName)));
        Check("the notice shows: the share has 1.0.1 and this PC runs 1.0.0", _mainVm.IsUpdateNoticeVisible && _mainVm.AvailableVersion == "1.0.1", _mainVm.UpdateNotice);
        Snapshot("stations-1-notice.png");

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
            db.StationVersions.Add(new StationVersion { StationName = "RECEPTION-2", AppVersion = "1.0.1", LastUserName = "Maria Santos", FirstSeenUtc = DateTime.UtcNow.AddDays(-3), LastSeenUtc = DateTime.UtcNow.AddMinutes(-4) });
            db.StationVersions.Add(new StationVersion { StationName = "LAB-OLD", AppVersion = "1.0.0", LastUserName = "Jose Reyes", FirstSeenUtc = DateTime.UtcNow.AddDays(-9), LastSeenUtc = DateTime.UtcNow.AddDays(-2) });
            db.SaveChangesAsync().GetAwaiter().GetResult();
        }

        var vm = Open<StationsViewModel>(ModuleIds.Stations);
        Pump(800);
        Snapshot("stations-2-list.png");
        Check("the list shows the three stations", vm.Stations.Count == 3, string.Join(" | ", vm.Stations.Select(s => $"{s.Station} {s.Version} {s.Status} {s.LastSeen}")));
        Check("RECEPTION-2 is up to date, the two on 1.0.0 are behind", vm.Stations.Single(s => s.Station == "RECEPTION-2").Status == "Up to date" && vm.Stations.Count(s => s.IsBehind) == 2, vm.Summary);
        Check("this PC is open now, the old one shows a date", vm.Stations.Single(s => s.Station == "HARNESS-PC").LastSeen == "Open now" && vm.Stations.Single(s => s.Station == "LAB-OLD").LastSeen.StartsWith("20"), "");
        Check("the summary says how many are behind", vm.Summary.Contains("2 of 3") && vm.Summary.Contains("1.0.1"), vm.Summary);

        Act(() => _mainVm.DismissUpdateCommand.Execute(null), 500);
        Check("Later hides the notice", !_mainVm.IsUpdateNoticeVisible, "");
        Snapshot("stations-3-dismissed.png");

        var signalled = false;
        _mainVm.RestartRequested += (_, _) => signalled = true;
        Act(() => _mainVm.RestartToUpdateCommand.Execute(null), 800);
        Check("Restart now hands over to the launcher and asks the window to close", FakeUpdates.Restarts == 1 && signalled, $"restarts {FakeUpdates.Restarts}");
    }

    private static List<StationVersion> Stored(IServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        return [.. scope.ServiceProvider.GetRequiredService<IAppDbContext>().StationVersions];
    }
}
