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
    ILogger<MainViewModel> logger) : ViewModelBase(logger)
{
    // Modules that exist in the new app. Everything else shows a "not migrated yet" page.
    private static readonly Dictionary<int, Func<IServiceProvider, object>> Pages = new()
    {
        [ModuleIds.Patients] = sp => sp.GetRequiredService<PatientsViewModel>(),
        [ModuleIds.Companies] = sp => sp.GetRequiredService<CompaniesViewModel>(),
        [ModuleIds.Departments] = sp => sp.GetRequiredService<DepartmentsViewModel>(),
        [ModuleIds.Services] = sp => sp.GetRequiredService<ServicesViewModel>(),
        [ModuleIds.Packages] = sp => sp.GetRequiredService<PackagesViewModel>(),
        [ModuleIds.Items] = sp => sp.GetRequiredService<ItemsViewModel>(),
        [ModuleIds.ItemLocations] = sp => sp.GetRequiredService<ItemLocationsViewModel>(),
        [ModuleIds.Discounts] = sp => sp.GetRequiredService<DiscountsViewModel>(),
    };

    public string Greeting => $"Signed in as {session.FullName}";

    public ObservableCollection<MenuGroup> Menu { get; } = [];

    [ObservableProperty]
    private object _currentPage = new PlaceholderViewModel("Welcome", "Pick a module from the menu on the left.");

    public void SignOut() => session.SignOut();

    [RelayCommand]
    private Task LoadMenuAsync() => RunAsync(async () =>
    {
        var groups = await runner.RunAsync<IMenuService, IReadOnlyList<MenuGroup>>(s => s.GetMenuAsync());
        Menu.Clear();
        foreach (var group in groups)
            Menu.Add(group);
    });

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
