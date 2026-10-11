using System.Windows;
using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Wpf.ViewModels;

namespace UiHarness;

internal static partial class Program
{
    private static void HomeChecks(IServiceProvider provider)
    {
        CapturePreview.OutputFolder = _out;
        _main.WindowState = WindowState.Normal;
        _main.Width = 1366;
        _main.Height = 728;
        _main.Left = 0;
        _main.Top = 0;
        Pump(1500);

        var home = _mainVm.CurrentPage as HomeViewModel;
        Check("the home list is the first page after signing in", home is not null, _mainVm.CurrentPage.GetType().Name);
        if (home is null)
            return;

        Pump(1500);
        Snapshot("home-1-today.png");
        Check("today shows no registration of the legacy data", home.TotalCount == 0 && home.HasNoRows, $"{home.TotalCount}");

        Act(() => home.AllDaysCommand.Execute(null), 1800);
        Check("all days lists the registrations, newest first, with their chips", home.Rows.Count >= 2 && home.Rows[0].Chips.Count > 0, $"{home.Rows.Count}");
        Snapshot("home-2-all.png");

        try
        {
            using var scope = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.CreateScope(provider);
            var board = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<DiagnosticLabs.Application.Board.IRegistrationBoardService>(scope.ServiceProvider);
            var direct = board.SearchAsync(new DiagnosticLabs.Application.Board.BoardSearch("EMMA")).GetAwaiter().GetResult();
            Console.WriteLine("DIRECT ok " + direct.IsSuccess + " " + (direct.IsSuccess ? direct.Value.TotalCount.ToString() : direct.Error.Message));
        }
        catch (Exception ex)
        {
            Console.WriteLine("DIRECT FAILED " + ex);
        }

        home.SearchText = "EMMA";
        Pump(1500);
        Check("typing searches after a pause", home.Rows.Count >= 1 && home.Rows.All(r => r.PatientName.Contains("EMMA")), $"{home.Rows.Count} rows, total {home.TotalCount}, busy {home.IsBusy}, msg {home.Message}");
        home.SearchText = string.Empty;
        Pump(1500);

        Act(() => home.PageSize = 10, 1200);
        Check("a page size smaller than the list pages it", home.TotalPages >= 1, $"{home.TotalPages}");

        var row = home.Rows.First(r => r.Chips.Any(c => c.CanOpen && !c.IsMade));
        var chip = row.Chips.First(c => c.CanOpen && !c.IsMade);
        Act(() => home.OpenChipCommand.Execute(chip), 3000);
        Check("a chip opens its result screen with the registration loaded", _mainVm.CurrentPage is ILabResultScreen && _mainVm.CurrentPage is not HomeViewModel, _mainVm.CurrentPage.GetType().Name);
        dynamic screen = _mainVm.CurrentPage;
        Check("the registration fills the new result", (bool)screen.HasRegistration && ((string)screen.PatientName).Length > 0 && (long)screen.Id == 0, (string)screen.PatientName);
        Snapshot("home-3-opened.png");

        Act(() => _mainVm.GoHomeCommand.Execute(null), 2500);
        var home2 = (HomeViewModel)_mainVm.CurrentPage;
        Act(() => home2.AllDaysCommand.Execute(null), 1800);
        var madeChip = home2.Rows.SelectMany(r => r.Chips).First(c => c.IsMade && c.CanOpen);
        Act(() => home2.OpenChipCommand.Execute(madeChip), 3000);
        dynamic opened = _mainVm.CurrentPage;
        Check("a ticked chip opens the result that was made", (long)opened.Id == madeChip.LatestResultId && (bool)opened.HasRegistration, $"{(long)opened.Id} vs {madeChip.LatestResultId}");
        Act(() => _mainVm.GoHomeCommand.Execute(null), 2500);
        Check("Home goes back to the list", _mainVm.CurrentPage is HomeViewModel, _mainVm.CurrentPage.GetType().Name);
    }
}
