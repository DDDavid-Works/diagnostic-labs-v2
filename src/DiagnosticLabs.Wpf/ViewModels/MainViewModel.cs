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

    [ObservableProperty]
    private object _currentPage = new PlaceholderViewModel("Welcome", "Pick a module from the menu on the left.");

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
        var groups = await runner.RunAsync<IMenuService, IReadOnlyList<MenuGroup>>(s => s.GetMenuAsync());
        Menu.Clear();
        foreach (var group in groups)
            Menu.Add(group);
    });

    /// <summary>Opens Payments with a registration already loaded (from "Pay now" on the registration screen).</summary>
    private void OpenPayment(long registrationId)
    {
        var payments = services.GetRequiredService<PaymentsViewModel>();
        payments.RequestRegistration(registrationId);
        CurrentPage = payments;
    }

    [RelayCommand]
    private void OpenModule(MenuItem? item)
    {
        if (item is null)
            return;

        CurrentPage = Pages.TryGetValue(item.ModuleId, out var factory)
            ? factory(services)
            : new PlaceholderViewModel(item.Name, "This module has not been migrated to the new app yet. Use the old app for now.");
    }
}
