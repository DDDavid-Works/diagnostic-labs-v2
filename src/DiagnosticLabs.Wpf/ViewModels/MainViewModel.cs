using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Board;
using DiagnosticLabs.Application.Menu;
using DiagnosticLabs.Application.Stations;
using DiagnosticLabs.Wpf.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DiagnosticLabs.Wpf.ViewModels;

public partial class MainViewModel(
    ICurrentUserSession session,
    IServiceRunner runner,
    IServiceProvider services,
    IPasswordChangeDialog passwordDialog,
    IDialogService dialogs,
    INavigationService navigation,
    IAppInfo appInfo,
    IUpdateChecker updates,
    ILogger<MainViewModel> logger) : ViewModelBase(logger)
{
    /// <summary>How often an open program reports itself to the Stations list and looks for a new release on the share.</summary>
    private static readonly TimeSpan MonitorInterval = TimeSpan.FromMinutes(10);

    // Modules that exist in the new app. Everything else shows a "not migrated yet" page.
    private static readonly Dictionary<int, Func<IServiceProvider, object>> Pages = new()
    {
        [ModuleIds.PatientRegistrations] = sp => sp.GetRequiredService<RegistrationsViewModel>(),
        [ModuleIds.StoolFecalysis] = sp => sp.GetRequiredService<StoolFecalysisViewModel>(),
        [ModuleIds.Urinalysis] = sp => sp.GetRequiredService<UrinalysisViewModel>(),
        [ModuleIds.Hematology] = sp => sp.GetRequiredService<HematologyViewModel>(),
        [ModuleIds.PhysicalExamination] = sp => sp.GetRequiredService<PhysicalExaminationViewModel>(),
        [ModuleIds.ClinicalMicroscopy] = sp => sp.GetRequiredService<ClinicalMicroscopyViewModel>(),
        [ModuleIds.MedicalExamReport] = sp => sp.GetRequiredService<MedicalExamReportViewModel>(),
        [ModuleIds.Serology] = sp => sp.GetRequiredService<SerologyViewModel>(),
        [ModuleIds.Immunology] = sp => sp.GetRequiredService<ImmunologyViewModel>(),
        [ModuleIds.PregnancyTest] = sp => sp.GetRequiredService<PregnancyTestViewModel>(),
        [ModuleIds.ClinicalChemistry] = sp => sp.GetRequiredService<ClinicalChemistryViewModel>(),
        [ModuleIds.ClinicalChemistry1] = sp => sp.GetRequiredService<ClinicalChemistry1ViewModel>(),
        [ModuleIds.ClinicalChemistry2] = sp => sp.GetRequiredService<ClinicalChemistry2ViewModel>(),
        [ModuleIds.AnnualPhysicalExam] = sp => sp.GetRequiredService<AnnualPhysicalExamViewModel>(),
        [ModuleIds.Payments] = sp => sp.GetRequiredService<PaymentsViewModel>(),
        [ModuleIds.Patients] = sp => sp.GetRequiredService<PatientsViewModel>(),
        [ModuleIds.Companies] = sp => sp.GetRequiredService<CompaniesViewModel>(),
        [ModuleIds.Departments] = sp => sp.GetRequiredService<DepartmentsViewModel>(),
        [ModuleIds.Services] = sp => sp.GetRequiredService<ServicesViewModel>(),
        [ModuleIds.Packages] = sp => sp.GetRequiredService<PackagesViewModel>(),
        [ModuleIds.Items] = sp => sp.GetRequiredService<ItemsViewModel>(),
        [ModuleIds.ItemLocations] = sp => sp.GetRequiredService<ItemLocationsViewModel>(),
        [ModuleIds.Discounts] = sp => sp.GetRequiredService<DiscountsViewModel>(),
        [ModuleIds.SalesReport] = sp => sp.GetRequiredService<SalesReportViewModel>(),
        [ModuleIds.BatchPrint] = sp => sp.GetRequiredService<BatchPrintViewModel>(),
        [ModuleIds.StatementOfAccounts] = sp => sp.GetRequiredService<StatementOfAccountViewModel>(),
        [ModuleIds.Users] = sp => sp.GetRequiredService<UsersViewModel>(),
        [ModuleIds.CompanySetup] = sp => sp.GetRequiredService<CompanySetupViewModel>(),
        [ModuleIds.FormDesigner] = sp => sp.GetRequiredService<FormDesignerViewModel>(),
        [ModuleIds.Stations] = sp => sp.GetRequiredService<StationsViewModel>(),
    };

    public string Greeting => $"Signed in as {session.FullName}";

    public ObservableCollection<MenuGroup> Menu { get; } = [];

    // The first thing after signing in is the list of today's registrations (it is created when the menu has loaded).
    [ObservableProperty]
    private object _currentPage = new PlaceholderViewModel("Welcome", "Pick a module from the menu on the left.");

    private bool _navigating;

    /// <summary>The module on screen can be closed to show the list of registrations again (not offered on the list itself).</summary>
    public bool CanCloseModule => CurrentPage is not HomeViewModel;

    partial void OnCurrentPageChanged(object value) => OnPropertyChanged(nameof(CanCloseModule));

    /// <summary>
    /// Whether the screen on show may be left: <c>true</c> when its form has no changes, or the user saved them or chose to leave them out.
    /// Everything that replaces the screen (another module, Home, closing it, signing out, closing the window) asks this first.
    /// </summary>
    public async Task<bool> CanLeaveAsync() => CurrentPage is not IDirtyForm form || await form.ConfirmLeaveAsync();

    private async Task NavigateAsync(Func<object> next)
    {
        if (_navigating)
            return;

        _navigating = true;
        try
        {
            if (await CanLeaveAsync())
                CurrentPage = next();
        }
        finally
        {
            _navigating = false;
        }
    }

    /// <summary>Back to the list of registrations (a fresh one, so it shows what has changed).</summary>
    [RelayCommand]
    private Task GoHomeAsync() => NavigateAsync(() => services.GetRequiredService<HomeViewModel>());

    /// <summary>Closes the module on show and shows the list of registrations.</summary>
    [RelayCommand]
    private Task CloseModuleAsync() => NavigateAsync(() => services.GetRequiredService<HomeViewModel>());

    /// <summary>The left menu can be hidden to give the working screen the full width (small monitors).</summary>
    [ObservableProperty]
    private bool _isMenuVisible = true;

    [RelayCommand]
    private void ToggleMenu() => IsMenuVisible = !IsMenuVisible;

    public void SignOut()
    {
        _monitor?.Cancel();
        session.SignOut();
    }

    // ---- new release notice ----

    private CancellationTokenSource? _monitor;

    /// <summary>The release on the server's share when it is not the one this program runs; <c>null</c> when this program is up to date or the share is unknown.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsUpdateNoticeVisible), nameof(UpdateNotice))]
    private string? _availableVersion;

    // "Later" hides the notice for that release only; a newer release shows it again.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsUpdateNoticeVisible))]
    private string? _dismissedVersion;

    public bool IsUpdateNoticeVisible => AvailableVersion is not null && AvailableVersion != DismissedVersion;

    public bool CanRestartToUpdate => updates.CanRestart;

    public string UpdateNotice => $"A new version of Diagnostic Labs ({AvailableVersion}) is ready; this PC runs {appInfo.Version}. Finish and save what you are doing, then restart to update.";

    /// <summary>Raised when the window should close so the launcher can install the update; the window closes without asking again.</summary>
    public event EventHandler? RestartRequested;

    [RelayCommand]
    private void DismissUpdate() => DismissedVersion = AvailableVersion;

    [RelayCommand]
    private async Task RestartToUpdateAsync()
    {
        // A form with changes that are not saved asks first, like every other way of leaving it.
        if (!updates.CanRestart || !await CanLeaveAsync())
            return;

        updates.StartRestart();
        RestartRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Reports this PC to the Stations list and looks at the share, now and every few minutes while the program is open.</summary>
    private void StartMonitoring()
    {
        _monitor?.Cancel();
        _monitor = new CancellationTokenSource();
        _ = MonitorAsync(_monitor.Token);
    }

    private async Task MonitorAsync(CancellationToken cancellationToken)
    {
        try
        {
            await CheckOnceAsync(cancellationToken);
            using var timer = new PeriodicTimer(MonitorInterval);
            while (await timer.WaitForNextTickAsync(cancellationToken))
                await CheckOnceAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Signed out or closed.
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "The station check stopped");
        }
    }

    private async Task CheckOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            await runner.RunAsync<IStationVersionService, bool>(async s =>
            {
                await s.RecordSeenAsync(cancellationToken);
                return true;
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Bookkeeping only: a database hiccup must never disturb the work on screen.
            logger.LogWarning(ex, "This station could not be recorded");
        }

        var latest = await updates.GetShareVersionAsync(cancellationToken);
        AvailableVersion = latest is not null && latest != appInfo.Version ? latest : null;
    }

    /// <summary>Available to every signed-in user from the header; it is not a permission module.</summary>
    [RelayCommand]
    private void ChangePassword()
    {
        if (passwordDialog.Show(forced: false))
            dialogs.Inform("Your password was changed.", "Change password");
    }

    [RelayCommand]
    private Task LoadMenuAsync() => RunAsync(async () =>
    {
        navigation.PaymentHandler = OpenPayment;
        navigation.ResultHandler = OpenResult;
        navigation.MenuChangedHandler = RefreshMenu;
        var groups = await runner.RunAsync<IMenuService, IReadOnlyList<MenuGroup>>(s => s.GetMenuAsync());
        Menu.Clear();
        foreach (var group in groups)
            Menu.Add(group);

        CurrentPage = services.GetRequiredService<HomeViewModel>();
        StartMonitoring();
    });

    /// <summary>The screen of a module: a built-in one, or the entry screen of a form made in the Custom Lab Form designer; <c>null</c> when it has none yet.</summary>
    private object? CreatePage(int moduleId, string? name)
    {
        if (Pages.TryGetValue(moduleId, out var factory))
            return factory(services);

        return LabResultModules.IsCustomForm(moduleId)
            ? ActivatorUtilities.CreateInstance<CustomFormViewModel>(services, moduleId, name ?? Menu.SelectMany(g => g.Items).FirstOrDefault(i => i.ModuleId == moduleId)?.Name ?? "Custom form")
            : null;
    }

    private void RefreshMenu() => _ = RefreshMenuAsync();

    /// <summary>Reloads the menu in place (a form was made, switched off or on); the screen on show stays.</summary>
    private async Task RefreshMenuAsync()
    {
        try
        {
            var groups = await runner.RunAsync<IMenuService, IReadOnlyList<MenuGroup>>(s => s.GetMenuAsync());
            Menu.Clear();
            foreach (var group in groups)
                Menu.Add(group);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "The menu could not be reloaded");
        }
    }

    /// <summary>Opens Payments with a registration already loaded (from "Pay now" on the registration screen).</summary>
    private void OpenPayment(long registrationId) => _ = NavigateAsync(() =>
    {
        var payments = services.GetRequiredService<PaymentsViewModel>();
        payments.RequestRegistration(registrationId);
        return payments;
    });

    /// <summary>Opens a result screen on a registration: the result that was made when there is one, otherwise a new result.</summary>
    private void OpenResult(int moduleId, long registrationId, long? resultId)
    {
        if (!Pages.ContainsKey(moduleId) && !LabResultModules.IsCustomForm(moduleId))
            return;

        _ = NavigateAsync(() =>
        {
            var page = CreatePage(moduleId, null)!;
            if (page is ILabResultScreen screen)
            {
                if (resultId is { } id)
                    screen.RequestResult(id);
                else
                    screen.RequestRegistration(registrationId);
            }

            return page;
        });
    }

    [RelayCommand]
    private Task OpenModuleAsync(MenuItem? item)
    {
        if (item is null)
            return Task.CompletedTask;

        return NavigateAsync(() => CreatePage(item.ModuleId, item.Name)
            ?? (object)new PlaceholderViewModel(item.Name, "This module has not been migrated to the new app yet. Use the old app for now."));
    }
}
