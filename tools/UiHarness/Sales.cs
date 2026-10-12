using System.IO;
using System.Windows;
using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Domain.Billing;
using DiagnosticLabs.Domain.Catalog;
using DiagnosticLabs.Domain.Patients;
using DiagnosticLabs.Domain.Registrations;
using DiagnosticLabs.Wpf.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace UiHarness;

internal static partial class Program
{
    // The Sales Report against sample data shaped like the client's sheet: 21 registrations and 9 services over two days, a company with a batch,
    // and a second, much bigger set (14 services, 70 registrations) that has to continue on further pages and in two sets of columns.
    private static void SalesChecks(IServiceProvider provider)
    {
        CapturePreview.OutputFolder = _out;
        _main.WindowState = WindowState.Normal;
        _main.Width = 1366;
        _main.Height = 728;
        _main.Left = 0;
        _main.Top = 0;

        var names = new[]
        {
            "REAN SCHWARZER", "EMMA MILLSTEIN", "LAURA S. ARSEID", "FIE CLAUSELL", "EMMA MILLSTEIN", "JUAN DELA CRUZ", "MARIA SANTOS", "PEDRO REYES", "ANA GARCIA", "JOSE RAMOS",
            "CARMEN TORRES", "MIGUEL FERNANDEZ", "ROSA MENDOZA", "ANTONIO VILLANUEVA", "LUZ AQUINO", "RICARDO GUTIERREZ", "ELENA CASTILLO", "FERNANDO MORALES", "TERESA BAUTISTA",
            "EDUARDO SORIANO", "GLORIA PASCUAL",
        };
        (string Name, decimal Price)[] sold =
        [
            ("Fecalysis", 120), ("Urinalysis", 110), ("Hematology", 300), ("Serology", 220), ("Pregnancy Test", 90),
            ("Clinical Chemistry", 150), ("Clinical Chemistry 1", 125), ("Clinical Chemistry 2", 330), ("Clinical Microscopy", 200),
        ];

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
            var services = sold.Select(s => new Service { ServiceName = s.Name, ServiceDescription = "d", Price = s.Price }).ToList();
            db.Services.AddRange(services);
            foreach (var setup in db.CompanySetups)
                setup.Logo = new FakeFiles().PickImage()!.Content; // the company logo, so the printout shows where it goes
            var company = new Company { CompanyName = "Harness Company" };
            db.Companies.Add(company);
            db.SaveChangesAsync().GetAwaiter().GetResult();

            for (var i = 0; i < names.Length; i++)
            {
                var registration = new PatientRegistration
                {
                    RegistrationCode = $"SALES-A-{i}",
                    Patient = new Patient { PatientCode = $"SA-{i}", PatientName = names[i] },
                    InputDate = LocalTime.ToUtc(new DateOnly(2026, 10, 10 + (i % 2)), new TimeSpan(9, i, 0)),
                    CompanyId = i < 5 ? company.Id : null,
                    BatchName = i < 5 ? "Batch A" : string.Empty,
                    AmountDue = i == 3 ? 1400m : sold.Sum(s => s.Price), // one registration is a bundle priced below its services
                    DiscountTotal = i % 4 == 0 ? 154.50m : 0m, // 10 percent off on some
                };
                for (var s = 0; s < services.Count; s++)
                    registration.Services.Add(new PatientRegistrationService { Service = services[s], Price = sold[s].Price });
                if (i >= 5 && i < 10)
                    registration.Payments.Add(new Payment { PaymentAmount = registration.AmountDue, Type = PaymentType.Charge, PaymentDate = DateTime.UtcNow });
                db.PatientRegistrations.Add(registration);
            }

            // The big set: 14 services, 70 registrations, each with a different handful of them.
            var many = Enumerable.Range(1, 14).Select(n => new Service { ServiceName = $"Harness Test Number {n}", ServiceDescription = "d", Price = 10m * n }).ToList();
            db.Services.AddRange(many);
            db.SaveChangesAsync().GetAwaiter().GetResult();
            for (var i = 0; i < 70; i++)
            {
                var registration = new PatientRegistration
                {
                    RegistrationCode = $"SALES-B-{i}",
                    Patient = new Patient { PatientCode = $"SB-{i}", PatientName = $"Bulk Patient {i + 1:00}" },
                    InputDate = LocalTime.ToUtc(new DateOnly(2026, 9, 1), new TimeSpan(8, 0, 0)).AddMinutes(i),
                    AmountDue = 1,
                };
                for (var s = 0; s < many.Count; s++)
                    if ((i + s) % 3 != 0)
                        registration.Services.Add(new PatientRegistrationService { Service = many[s], Price = i % 7 == 0 && s == 0 ? 10.5m : many[s].Price });
                db.PatientRegistrations.Add(registration);
            }

            db.SaveChangesAsync().GetAwaiter().GetResult();
        }

        var vm = Open<SalesReportViewModel>(ModuleIds.SalesReport);
        Pump(1000);
        Snapshot("sales-1-screen.png");
        Check("the screen lists the companies", vm.Companies.Any(c => c.Name == "Harness Company"), $"{vm.Companies.Count}");
        Check("the batch list waits for a company", !vm.CanPickBatch && vm.Batches.Count == 1, string.Join(",", vm.Batches));

        var harnessCompany = vm.Companies.First(c => c.Name == "Harness Company").Id;
        Act(() => vm.CompanyId = harnessCompany, 1500);
        Check("a company offers its batches", vm.CanPickBatch && vm.Batches.Contains("Batch A"), $"{vm.CanPickBatch} [{string.Join(",", vm.Batches)}] {vm.CompanyId} {vm.Message}");
        Snapshot("sales-2-company.png");
        Act(() => vm.CompanyId = 0, 800);
        Pump(500);

        // The client's sheet: two days, everyone.
        vm.DateFrom = new DateTime(2026, 10, 10);
        vm.DateTo = new DateTime(2026, 10, 11);
        Act(() => vm.PrintCommand.Execute(null), 3000);
        var document = CapturePreview.LastDocument;
        Check("print opens the report", document is not null, "");
        Check("the client-sized report is one landscape page", document?.Pages.Count == 1 && document.DocumentPaginator.PageSize.Width > document.DocumentPaginator.PageSize.Height, $"{document?.Pages.Count}");

        // Direct payments only, then what was charged to the company: the same sheet, less and more of it.
        Act(() => vm.Payment = SalesReportViewModel.PaymentChoices[1], 300);
        CapturePreview.Clear();
        Act(() => vm.PrintCommand.Execute(null), 3000);
        Check("direct payments print", CapturePreview.LastDocument is { Pages.Count: 1 }, "");
        Act(() => vm.Payment = SalesReportViewModel.PaymentChoices[2], 300);
        CapturePreview.Clear();
        Act(() => vm.PrintCommand.Execute(null), 3000);
        Check("charged to company prints", CapturePreview.LastDocument is { Pages.Count: 1 }, "");
        Act(() => vm.Payment = SalesReportViewModel.PaymentChoices[0], 300);

        // A period with no sales says so instead of printing an empty sheet.
        CapturePreview.Clear();
        vm.DateFrom = new DateTime(2026, 1, 1);
        vm.DateTo = new DateTime(2026, 1, 2);
        Act(() => vm.PrintCommand.Execute(null), 2000);
        Check("an empty period prints nothing and says so", CapturePreview.LastDocument is null && vm.Message.Contains("no sales"), vm.Message);

        vm.DateFrom = new DateTime(2026, 10, 12);
        vm.DateTo = new DateTime(2026, 10, 11);
        Act(() => vm.PrintCommand.Execute(null), 2000);
        Check("a to-date before the from-date is refused", CapturePreview.LastDocument is null && vm.IsError, vm.Message);

        // The big one: pages and two sets of columns.
        vm.DateFrom = new DateTime(2026, 9, 1);
        vm.DateTo = new DateTime(2026, 9, 1);
        Act(() => vm.PrintCommand.Execute(null), 5000);
        Check("a big report continues over several pages", CapturePreview.LastDocument is { Pages.Count: > 2 }, $"{CapturePreview.LastDocument?.Pages.Count}");
        Console.WriteLine($"big report pages: {CapturePreview.LastDocument?.Pages.Count}");

        // Copy the one-page report's PDF name for the reader of the output folder.
        Console.WriteLine("files: " + string.Join(", ", Directory.GetFiles(_out).Select(Path.GetFileName)));
    }
}
