using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Menu;
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
    ILogger<MainViewModel> logger) : ViewModelBase(logger)
{
    // Modules that exist in the new app. Everything else shows a "not migrated yet" page.
    private static readonly Dictionary<int, Func<IServiceProvider, object>> Pages = new()
    {
        [ModuleIds.PatientRegistrations] = sp => sp.GetRequiredService<RegistrationsViewModel>(),
        [ModuleIds.StoolFecalysis] = sp => sp.GetRequiredService<StoolFecalysisViewModel>(),
        [ModuleIds.Urinalysis] = sp => sp.GetRequiredService<UrinalysisViewModel>(),
        [ModuleIds.Hematology] = sp => sp.GetRequiredService<HematologyViewModel>(),
        [ModuleIds.Serology] = sp => sp.GetRequiredService<SerologyViewModel>(),
        [ModuleIds.Immunology] = sp => sp.GetRequiredService<ImmunologyViewModel>(),
        [ModuleIds.PregnancyTest] = sp => sp.GetRequiredService<PregnancyTestViewModel>(),
        [ModuleIds.ClinicalChemistry] = sp => sp.GetRequiredService<ClinicalChemistryViewModel>(),
        [ModuleIds.ClinicalChemistry1] = sp => sp.GetRequiredService<ClinicalChemistry1ViewModel>(),
        [ModuleIds.ClinicalChemistry2] = sp => sp.GetRequiredService<ClinicalChemistry2ViewModel>(),
        [ModuleIds.AnnualPhysicalExamPage2] = sp => sp.GetRequiredService<MedicalExaminationViewModel>(),
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
        [ModuleIds.Users] = sp => sp.GetRequiredService<UsersViewModel>(),
        [ModuleIds.CompanySetup] = sp => sp.GetRequiredService<CompanySetupViewModel>(),
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

    public void SignOut() => session.SignOut();

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
        var groups = await runner.RunAsync<IMenuService, IReadOnlyList<MenuGroup>>(s => s.GetMenuAsync());
        Menu.Clear();
        foreach (var group in groups)
            Menu.Add(group);

        CurrentPage = services.GetRequiredService<HomeViewModel>();
    });

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
        if (!Pages.TryGetValue(moduleId, out var factory))
            return;

        _ = NavigateAsync(() =>
        {
            var page = factory(services);
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

        return NavigateAsync(() => Pages.TryGetValue(item.ModuleId, out var factory)
            ? factory(services)
            : new PlaceholderViewModel(item.Name, "This module has not been migrated to the new app yet. Use the old app for now."));
    }
}
