using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Registrations;
using DiagnosticLabs.Wpf.ViewModels;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace UiHarness;

internal static partial class Program
{
    private static void RegistrationChecks(IServiceProvider provider)
    {
        _main.WindowState = WindowState.Normal;
        _main.Width = 1366;
        _main.Height = 728;
        _main.Left = 0;
        _main.Top = 0;
        Pump(800);

        SeedPackages();
        var vm = Open<RegistrationsViewModel>(ModuleIds.PatientRegistrations);        Pump(1500);
        Check("the registration code preview is shown", vm.RegistrationCode.StartsWith("BADC-"), vm.RegistrationCode);
        Check("the patient code preview is shown", vm.PatientCode.Length == 13 && vm.PatientCode[4] == '-', vm.PatientCode);
        Check("genders, companies and services loaded", vm.Genders.Count > 0 && vm.Companies.Count >= 2 && vm.ServiceOptions.Count > 3, $"{vm.Genders.Count}/{vm.Companies.Count}/{vm.ServiceOptions.Count}");
        Snapshot("reg-1-new-menu.png");

        // as-you-type matching of existing patients
        Act(() => vm.PatientName = "Ars", 1500);
        Check("matching existing patients are offered while typing", vm.Suggestions.Count > 0 && vm.HasSuggestions, $"{vm.Suggestions.Count}: {string.Join(", ", vm.Suggestions.Select(x => x.PatientName))}");
        var pick = vm.Suggestions.First();
        Act(() => vm.PickSuggestionCommand.Execute(pick), 1500);
        Check("picking one fills in the patient", vm.PatientId == pick.Id && vm.PatientName == pick.PatientName && vm.IsExistingPatient && vm.CanChangePatient && !vm.HasSuggestions, $"{vm.PatientName} / {vm.PatientCode}");
        Snapshot("reg-1b-existing-patient.png");

        Act(() => vm.ChangePatientCommand.Execute(null), 1200);
        Check("'different patient' lets go of them", vm.PatientId == 0 && vm.PatientName.Length == 0 && !vm.CanChangePatient, vm.PatientCode);

        vm.PatientName = "Harness Walkin";
        vm.DateOfBirth = new DateTime(1990, 5, 17);
        vm.Sex = "Female";
        Act(() => vm.SaveCommand.Execute(null), 2500);
        Check("saving without a service is refused", vm.IsError && vm.Message.Contains("service"), vm.Message);

        // services via the picker; the price follows
        var (a, b) = (vm.ServiceOptions[0], vm.ServiceOptions[1]);
        ScriptedPicker.Answer = (_, _) => [a.Id, b.Id];
        Act(() => vm.ChooseServicesCommand.Execute(null));
        Check("picked services appear and the price is their total", vm.Services.Count == 2 && vm.AmountDue == a.Price + b.Price, $"{vm.AmountDue}");
        vm.Services[0].Price = 10m;
        Check("a line price edit moves the total", vm.AmountDue == 10m + b.Price, $"{vm.AmountDue}");
        Snapshot("reg-2-filled-menu.png");

        Act(() => _mainVm.ToggleMenuCommand.Execute(null));
        Pump(500);
        Snapshot("reg-3-filled-nomenu.png");
        Act(() => _mainVm.ToggleMenuCommand.Execute(null));

        Act(() => vm.SaveCommand.Execute(null), 3000);
        Check("registration saved with a real code", vm.Id != 0 && !vm.IsError && vm.RegistrationCode.StartsWith("BADC-") && vm.Message.Contains(vm.RegistrationCode), $"{vm.RegistrationCode} / {vm.Message}");
        Check("a new patient was created with a real code", vm.PatientId != 0 && vm.PatientCode.Length == 13, vm.PatientCode);
        var firstCode = vm.RegistrationCode;
        var firstId = vm.Id;
        Snapshot("reg-4-saved.png");

        // second registration for the same, now existing, patient via Save & New
        var existing = vm.PatientName;
        Act(() => vm.SaveAndNewCommand.Execute(null), 3000);
        Check("Save & New starts an empty form and keeps the saved message", vm.IsNew && vm.PatientName.Length == 0 && vm.Services.Count == 0 && !vm.IsError, vm.Message);

        // a general package (no company) replaces the services and sets the price
        var generalPackage = vm.Packages.FirstOrDefault(p => p.Id != 0);
        if (generalPackage is not null)
        {
            Act(() => vm.PackageChoiceId = generalPackage.Id, 1500);
            Check("a general package fills in its services and price", vm.Services.Count > 0 && vm.AmountDue == generalPackage.Price, $"{vm.Services.Count} services, {vm.AmountDue} vs {generalPackage.Price}");
            Act(() => vm.PackageChoiceId = 0, 300);
            Act(() => vm.NewRecordCommand.Execute(null), 1200);
        }
        else
        {
            Console.WriteLine("  (no general package in the data)");
        }

        // a package of a company is offered only with that company
        var withPackage = FindCompanyWithPackage(provider);
        if (withPackage is { } company)
        {
            var generalCount = vm.Packages.Count;
            Act(() => vm.CompanyChoiceId = company.Id, 1500);
            var companyPackages = vm.Packages.Where(p => p.Id != 0).ToList();
            Check("choosing a company shows only that company's packages", companyPackages.Count > 0 && vm.PackageChoiceId == 0, $"{companyPackages.Count} packages (general had {generalCount - 1})");
            Act(() => vm.PackageChoiceId = companyPackages[0].Id, 1500);
            Check("picking a package replaces the services and sets its price", vm.Services.Count > 0 && vm.AmountDue == companyPackages[0].Price, $"{vm.Services.Count} services, {vm.AmountDue} vs {companyPackages[0].Price}");
            vm.PatientName = "Harness Packaged";
            vm.Age = "41";
            vm.BatchName = "Harness Batch";
            Act(() => vm.SaveCommand.Execute(null), 3000);
            Check("package registration saved", vm.Id != 0 && !vm.IsError, vm.Message);
            Check("typed age kept when no birth date", vm.Age == "41" && vm.DateOfBirth is null, vm.Age);
            Act(() => vm.CompanyChoiceId = 0, 1500);
            Check("removing the company resets the package", vm.PackageChoiceId == 0, "");
            Snapshot("reg-5-package.png");
        }
        else
        {
            Console.WriteLine("  (no company with packages in the data; package flow skipped)");
        }

        // list + filters
        Act(() => vm.ToggleSearchCommand.Execute(null));
        Act(() => vm.SearchText = string.Empty);
        Act(() => vm.SearchCommand.Execute(null));
        Check("registrations are listed, newest first", vm.Items.Count >= 2 && vm.Items[0].Id > vm.Items[^1].Id, $"{vm.Items.Count} items");
        Snapshot("reg-6-list.png");
        Act(() => vm.DateFilter = DateTime.Today, 1500);
        var todayCount = vm.Items.Count;
        Act(() => vm.DateFilter = DateTime.Today.AddDays(-30), 1500);
        Check("the date filter narrows the list", todayCount >= 1 && vm.Items.Count == 0, $"today {todayCount}, 30 days ago {vm.Items.Count}");
        Act(() => vm.ClearFiltersCommand.Execute(null), 1500);

        // open and edit the first registration; it keeps its code
        Act(() => vm.SelectedItem = vm.Items.First(i => i.Id == firstId), 1500);
        Check("opening a saved registration shows its code and services", vm.RegistrationCode == firstCode && vm.Services.Count == 2 && vm.AmountDue == 10m + b.Price, $"{vm.RegistrationCode} {vm.Services.Count} {vm.AmountDue}");
        Check("the patient of a saved registration can not be swapped", !vm.CanChangePatient, "");
        vm.Services[1].Price = 20m;
        Act(() => vm.SaveCommand.Execute(null), 3000);
        Check("edited and saved", !vm.IsError && vm.Services[1].Price == 20m && vm.RegistrationCode == firstCode, vm.Message);
        Snapshot("reg-7-edited.png");

        Check("Pay now is offered on a saved registration", vm.CanPayNow, "");
        var openId = vm.Id;
        Act(() => vm.PayNowCommand.Execute(null), 3500);
        var payments = _mainVm.CurrentPage as PaymentsViewModel;
        Check("Pay now opens Payments with the registration loaded and its balance ready", payments is not null && payments.HasRegistration && payments.Registration!.RegistrationId == openId && payments.PaymentAmount == payments.BalanceBefore, $"{payments?.Registration?.RegistrationCode} {payments?.PaymentAmount}");
        Snapshot("reg-8-paynow.png");
        // back to the registration to delete it
        Open<RegistrationsViewModel>(ModuleIds.PatientRegistrations);
        vm = (RegistrationsViewModel)_mainVm.CurrentPage;
        Act(() => vm.SelectedItem = vm.Items.First(i => i.Id == openId), 1500);
        Act(() => vm.NewRecordCommand.Execute(null), 600);
        Check("Pay now is hidden on a new registration", !vm.CanPayNow, "");
        Act(() => vm.SelectedItem = vm.Items.First(i => i.Id == openId), 1500);
        Act(() => vm.DeleteCommand.Execute(null), 2500);
        Check("a registration without payments can be deleted", !vm.IsError && vm.Id == 0, vm.Message);
    }

    // Registrations need a company that has a package and a general package to pick from.
    private static void SeedPackages()
    {
        var companies = Open<CompaniesViewModel>(ModuleIds.Companies);
        Act(() => companies.NewRecordCommand.Execute(null));
        companies.CompanyName = "Harness Corp";
        Act(() => companies.SaveCommand.Execute(null), 2500);

        var packages = Open<PackagesViewModel>(ModuleIds.Packages);
        var options = packages.ServiceOptions.ToList();
        foreach (var (name, companyName, picks) in new[] { ("Harness General", "", new[] { 0, 1, 2 }), ("Harness Corp Package", "Harness Corp", new[] { 2, 3 }) })
        {
            Act(() => packages.NewRecordCommand.Execute(null));
            packages.PackageName = name;
            packages.PackageDescription = "d";
            if (companyName.Length > 0)
                packages.CompanyChoiceId = packages.Companies.First(c => c.Name == companyName).Id;
            ScriptedPicker.Answer = (_, _) => [.. picks.Select(i => options[i].Id)];
            Act(() => packages.ChooseServicesCommand.Execute(null));
            packages.Price = options[picks[0]].Price + 1m;
            Act(() => packages.SaveCommand.Execute(null), 2500);
            Check($"seeded package '{name}'", packages.Id != 0 && !packages.IsError, packages.Message);
        }
    }
    private static DiagnosticLabs.Application.Lookups.LookupOption? FindCompanyWithPackage(IServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        var lookups = scope.ServiceProvider.GetRequiredService<DiagnosticLabs.Application.Lookups.IReferenceLookups>();
        return Task.Run(async () =>
        {
            foreach (var company in await lookups.GetCompaniesAsync())
                if ((await lookups.GetPackagesAsync(company.Id)).Count > 0)
                    return company;
            return null;
        }).GetAwaiter().GetResult();
    }
}
