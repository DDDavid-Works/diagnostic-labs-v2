using System.IO;
using System.Windows;
using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Domain.Catalog;
using DiagnosticLabs.Domain.Lab;
using DiagnosticLabs.Domain.Patients;
using DiagnosticLabs.Domain.Registrations;
using DiagnosticLabs.Wpf.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace UiHarness;

internal static partial class Program
{
    // Batch Print against a company with two batches of three patients each: four have a Hematology result (one has two), two have none yet.
    // Run without -StartAt: the check adds the two modules it needs (the migration that adds Batch Print is proved on its own).
    private static void BatchPrintChecks(IServiceProvider provider)
    {
        CapturePreview.OutputFolder = _out;
        _main.WindowState = WindowState.Normal;
        _main.Width = 1366;
        _main.Height = 728;
        _main.Left = 0;
        _main.Top = 0;

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
            foreach (var setup in db.CompanySetups)
                setup.Logo = new FakeFiles().PickImage()!.Content;
            var hematology = new Service { ServiceName = "Hematology", ServiceDescription = "d", Price = 300m };
            var company = new Company { CompanyName = "Harness Company" };
            db.Services.Add(hematology);
            db.Companies.Add(company);
            db.SaveChangesAsync().GetAwaiter().GetResult();
            var type = new DiagnosticLabs.Domain.Identity.ModuleType { Id = 3, ModuleTypeName = "Lab Results", Icon = "folder", SortOrder = 1, IsActive = true };
            db.ModuleTypes.Add(type);
            db.Modules.Add(new DiagnosticLabs.Domain.Identity.Module { Id = ModuleIds.Hematology, ModuleType = type, ModuleName = "Hematology", HasView = true, HasPrint = true, IsActive = true, ServiceId = hematology.Id });
            db.Modules.Add(new DiagnosticLabs.Domain.Identity.Module { Id = ModuleIds.BatchPrint, ModuleType = type, ModuleName = "Batch Print", HasView = true, HasPrint = true, IsActive = true });
            db.SaveChangesAsync().GetAwaiter().GetResult();

            var people = new (string Name, string Batch, int Results)[]
            {
                ("ZITA REYES", "Batch 1", 1), ("ALBERT CRUZ", "Batch 1", 2), ("MARIA SANTOS", "Batch 1", 0),
                ("JUAN DELA CRUZ", "Batch 2", 1), ("ANA GARCIA", "Batch 2", 1), ("PEDRO LIM", "Batch 2", 0),
            };
            foreach (var (name, batch, results) in people)
            {
                var registration = new PatientRegistration
                {
                    RegistrationCode = "BP-" + name[..3] + batch[^1],
                    Patient = new Patient { PatientCode = "BP-" + name[..3], PatientName = name },
                    InputDate = DateTime.UtcNow, CompanyId = company.Id, BatchName = batch, AmountDue = 300,
                };
                registration.Services.Add(new PatientRegistrationService { Service = hematology, Price = 300 });
                db.PatientRegistrations.Add(registration);
                db.SaveChangesAsync().GetAwaiter().GetResult();
                for (var i = 0; i < results; i++)
                {
                    db.LabReports.Add(new LabReport
                    {
                        ReportType = LabReportType.Hematology, PatientRegistrationId = registration.Id, PatientId = registration.PatientId,
                        PatientCode = registration.Patient.PatientCode, PatientName = name, DateRequested = DateTime.UtcNow,
                        MedicalTechnologist = "Harness Tech", Pathologist = "Harness Doctor", Remarks = i == 0 && results > 1 ? "older result" : "latest result",
                    });
                    db.SaveChangesAsync().GetAwaiter().GetResult();
                }
            }
        }

        var vm = Open<BatchPrintViewModel>(ModuleIds.BatchPrint);
        Pump(1000);
        Snapshot("batch-1-screen.png");
        Check("the screen lists the companies and the services with a form", vm.Companies.Any(c => c.Name == "Harness Company") && vm.Services.Any(s => s.Name == "Hematology"), $"{vm.Companies.Count} companies, {vm.Services.Count} services");
        Check("nothing is picked at first (company and service are required)", vm.CompanyId == 0 && vm.ServiceId == 0 && !vm.CanPickBatch, "");

        Check("Print is off until a search found results", !vm.PrintCommand.CanExecute(null), "");
        Act(() => vm.SearchCommand.Execute(null), 1500);
        Check("searching without a company and a service is refused", !vm.HasRows && vm.IsError && vm.Message.Contains("company") && vm.Message.Contains("service"), vm.Message);

        var companyId = vm.Companies.First(c => c.Name == "Harness Company").Id;
        var serviceId = vm.Services.First(s => s.Name == "Hematology").ServiceId;
        Act(() => vm.CompanyId = companyId, 1500);
        Act(() => vm.ServiceId = serviceId, 300);
        Check("the company offers its batches", vm.Batches.Contains("Batch 1") && vm.Batches.Contains("Batch 2"), string.Join(",", vm.Batches));

        Act(() => vm.SearchCommand.Execute(null), 2500);
        Check("the search lists every patient of the company with the service, sorted by name",
            vm.Rows.Select(r => r.PatientName).SequenceEqual(["ALBERT CRUZ", "ANA GARCIA", "JUAN DELA CRUZ", "MARIA SANTOS", "PEDRO LIM", "ZITA REYES"]), string.Join(",", vm.Rows.Select(r => r.PatientName)));
        Check("each row says whether the result is inputted and when", vm.Rows.Count(r => r.HasResult) == 4 && vm.Rows.Where(r => r.HasResult).All(r => r.DateText.Length > 0) && vm.Rows.Where(r => !r.HasResult).All(r => r.DateText.Length == 0 && r.ResultText == "No"),
            string.Join(" | ", vm.Rows.Select(r => $"{r.PatientName}:{r.ResultText}:{r.DateText}")));
        Check("a summary says how many have results", vm.Summary.Contains("6 patients") && vm.Summary.Contains("4 with results") && vm.Summary.Contains("2 without"), vm.Summary);
        Check("nothing is printed by searching", CapturePreview.LastDocument is null, "");
        Check("Print is on once there are results", vm.PrintCommand.CanExecute(null), "");
        Snapshot("batch-2-searched.png");

        Act(() => vm.PrintCommand.Execute(null), 4000);
        var doc = CapturePreview.LastDocument;
        Check("Print prints one page per patient with a result, none for the others", doc?.Pages.Count == 4, $"{doc?.Pages.Count} pages");

        Act(() => vm.Batch = "Batch 1", 300);
        Check("changing a parameter clears the grid and turns Print off", !vm.HasRows && vm.Summary.Length == 0 && !vm.PrintCommand.CanExecute(null), "");
        CapturePreview.Clear();
        Act(() => vm.SearchCommand.Execute(null), 2500);
        Check("one batch lists only its patients", vm.Rows.Count == 3 && vm.Rows.Count(r => r.HasResult) == 2, $"{vm.Rows.Count} rows");
        Act(() => vm.PrintCommand.Execute(null), 4000);
        Check("and prints only theirs", CapturePreview.LastDocument?.Pages.Count == 2, $"{CapturePreview.LastDocument?.Pages.Count} pages");

        // A date range that holds none of the results marks every patient as having none, and there is nothing to print.
        Act(() => vm.DateFrom = new DateTime(2020, 1, 1), 300);
        Act(() => vm.DateTo = new DateTime(2020, 1, 2), 300);
        Act(() => vm.SearchCommand.Execute(null), 2500);
        Check("a range without results lists the patients as having none", vm.Rows.Count == 3 && vm.Rows.All(r => !r.HasResult) && !vm.PrintCommand.CanExecute(null), $"{vm.Rows.Count} rows");
        Snapshot("batch-3-no-results.png");
        Console.WriteLine("files: " + string.Join(", ", Directory.GetFiles(_out).Select(Path.GetFileName)));
    }
}
