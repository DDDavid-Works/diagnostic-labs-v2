using System.IO;
using System.Windows;
using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Domain.Billing;
using DiagnosticLabs.Domain.Patients;
using DiagnosticLabs.Domain.Registrations;
using DiagnosticLabs.Wpf.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace UiHarness;

internal static partial class Program
{
    // Statement of Accounts against the client's two sheets: SM CLARK with 21 charged registrations (gross 7,965, discount 260, net 7,705) and Emma
    // Millstein with 11 registrations (gross 4,200, discount 60, net 4,140, paid 2,890, balance 1,250). A second company with 90 charged registrations
    // has to continue over several pages.
    private static void StatementChecks(IServiceProvider provider)
    {
        CapturePreview.OutputFolder = _out;
        _main.WindowState = WindowState.Normal;
        _main.Width = 1366;
        _main.Height = 728;
        _main.Left = 0;
        _main.Top = 0;

        var day = new DateOnly(2026, 10, 10);
        (string Name, decimal Gross, decimal Discount)[] clark =
        [
            ("REAN SCHWARZER", 400, 0), ("EMMA MILLSTEIN", 400, 0), ("LAURA S. ARSEID", 400, 0), ("FIE CLAUSELL", 400, 0), ("EMMA MILLSTEIN", 400, 15), ("JUAN DELA CRUZ", 400, 0),
            ("MARIA SANTOS", 350, 0), ("PEDRO REYES", 350, 0), ("ANA GARCIA", 350, 0), ("JOSE RAMOS", 350, 0), ("CARMEN TORRES", 400, 45), ("MIGUEL FERNANDEZ", 400, 0),
            ("ROSA MENDOZA", 320, 60), ("ANTONIO VILLANUEVA", 290, 0), ("LUZ AQUINO", 400, 20), ("RICARDO GUTIERREZ", 400, 20), ("ELENA CASTILLO", 400, 0),
            ("FERNANDO MORALES", 400, 50), ("TERESA BAUTISTA", 400, 0), ("EDUARDO SORIANO", 355, 50), ("GLORIA PASCUAL", 400, 0),
        ];
        (decimal Gross, decimal Discount, decimal Paid)[] emma =
        [
            (400, 0, 0), (400, 0, 400), (400, 0, 250), (400, 0, 400), (400, 15, 385), (400, 0, 400), (350, 0, 350), (350, 0, 0), (350, 0, 0), (350, 0, 350), (400, 45, 355),
        ];

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
            foreach (var setup in db.CompanySetups)
                setup.Logo = new FakeFiles().PickImage()!.Content;
            var smClark = new Company { CompanyName = "SM CLARK" };
            var bulk = new Company { CompanyName = "Big Company Inc." };
            db.Companies.AddRange(smClark, bulk);
            db.SaveChangesAsync().GetAwaiter().GetResult();

            for (var i = 0; i < clark.Length; i++)
            {
                var registration = new PatientRegistration
                {
                    RegistrationCode = $"BADC-10OC26-{i + 100:00000}",
                    Patient = new Patient { PatientCode = $"SM-{i}", PatientName = clark[i].Name },
                    InputDate = LocalTime.ToUtc(day, new TimeSpan(9, i, 0)),
                    CompanyId = smClark.Id,
                    BatchName = "Batch 1",
                    AmountDue = clark[i].Gross,
                    DiscountTotal = clark[i].Discount,
                };
                registration.Payments.Add(new Payment { PaymentAmount = 0m, Type = PaymentType.Charge, PaymentDate = DateTime.UtcNow });
                db.PatientRegistrations.Add(registration);
            }

            // Not charged to the company, so they stay off its statement.
            db.PatientRegistrations.Add(new PatientRegistration
            {
                RegistrationCode = "BADC-10OC26-00999",
                Patient = new Patient { PatientCode = "SM-PAID", PatientName = "PAID BY HERSELF" },
                InputDate = LocalTime.ToUtc(day, new TimeSpan(10, 0, 0)),
                CompanyId = smClark.Id,
                BatchName = "Batch 1",
                AmountDue = 400m,
            });

            var patient = new Patient { PatientCode = "2026-00000001", PatientName = "EMMA MILLSTEIN" };
            for (var i = 0; i < emma.Length; i++)
            {
                var registration = new PatientRegistration
                {
                    RegistrationCode = $"BADC-10OC26-{i + 1:00000}",
                    Patient = patient,
                    InputDate = LocalTime.ToUtc(day, new TimeSpan(8, i, 0)),
                    AmountDue = emma[i].Gross,
                    DiscountTotal = emma[i].Discount,
                };
                if (emma[i].Paid > 0)
                    registration.Payments.Add(new Payment { PaymentAmount = emma[i].Paid, Type = PaymentType.Payment, PaymentDate = DateTime.UtcNow });
                db.PatientRegistrations.Add(registration);
            }

            for (var i = 0; i < 90; i++)
            {
                var registration = new PatientRegistration
                {
                    RegistrationCode = $"BIG-{i:0000}",
                    Patient = new Patient { PatientCode = $"BIG-{i}", PatientName = $"Employee Number {i + 1:000}" },
                    InputDate = LocalTime.ToUtc(day.AddDays(i % 5), new TimeSpan(8, 0, 0)).AddMinutes(i),
                    CompanyId = bulk.Id,
                    BatchName = i % 2 == 0 ? "Batch A" : "Batch B",
                    AmountDue = 300m + (i % 4 * 50),
                    DiscountTotal = i % 9 == 0 ? 25m : 0m,
                };
                registration.Payments.Add(new Payment { PaymentAmount = 0m, Type = PaymentType.Charge, PaymentDate = DateTime.UtcNow });
                db.PatientRegistrations.Add(registration);
            }

            db.SaveChangesAsync().GetAwaiter().GetResult();
        }

        var vm = Open<StatementOfAccountViewModel>(ModuleIds.StatementOfAccounts);
        Pump(1000);
        Snapshot("statement-1-screen.png");
        Check("the screen lists the companies", vm.Companies.Any(c => c.Name == "SM CLARK"), $"{vm.Companies.Count}");
        Check("it starts in company mode with nothing to generate", vm.IsCompanyMode && !vm.IsPatientMode && !vm.GenerateCommand.CanExecute(null), "");

        Act(() => vm.PreviewCommand.Execute(null), 1500);
        Check("previewing without a company is refused", vm.IsError && vm.Message.Contains("company") && !vm.HasLines, vm.Message);

        // ---- the client's company sheet
        var companyId = vm.Companies.First(c => c.Name == "SM CLARK").Id;
        Act(() => vm.CompanyId = companyId, 1500);
        Check("a company offers the batches that have charged registrations", vm.Batches.Contains("Batch 1") && vm.CanPickBatch, string.Join(",", vm.Batches));
        Act(() => vm.Attention = "MR. FRANZ HILL", 200);
        Act(() => vm.DateFrom = new DateTime(2026, 10, 10), 200);
        Act(() => vm.DateTo = new DateTime(2026, 10, 15), 200);
        Act(() => vm.PreviewCommand.Execute(null), 2500);
        Check("the preview lists the 21 charged registrations, not the one paid directly", vm.Lines.Count == 21 && vm.Lines.All(l => l.PatientName != "PAID BY HERSELF"), $"{vm.Lines.Count}");
        Check("the summary has the sheet's totals", vm.Summary.Contains("7,965.00") && vm.Summary.Contains("260.00") && vm.Summary.Contains("7,705.00"), vm.Summary);
        Check("nothing is saved by previewing", vm.Saved.Count == 0 && CapturePreview.LastDocument is null, "");
        Snapshot("statement-2-company-preview.png");

        Act(() => vm.GenerateCommand.Execute(null), 4000);
        var doc = CapturePreview.LastDocument;
        Check("Generate saves the statement and opens its printout", doc?.Pages.Count == 1 && vm.Saved.Count == 1 && vm.Message.Contains("SOA-"), $"{doc?.Pages.Count} pages, {vm.Saved.Count} saved, {vm.Message}");
        Snapshot("statement-2b-saved-message.png");
        Check("the saved message is not shown as an error", !vm.IsError, vm.Message);
        Check("the preview is cleared once it is saved", !vm.HasLines && !vm.GenerateCommand.CanExecute(null), "");
        Console.WriteLine("files: " + string.Join(", ", Directory.GetFiles(_out).Select(Path.GetFileName)));

        // ---- the client's patient sheet
        Act(() => vm.IsPatientMode = true, 300);
        CapturePreview.Clear();
        Act(() => vm.PreviewCommand.Execute(null), 1500);
        Check("previewing without a patient is refused", vm.IsError && vm.Message.Contains("patient"), vm.Message);
        Act(() => vm.PatientText = "Emma", 1500);
        Check("typing a name offers matching patients", vm.HasSuggestions && vm.Suggestions.Any(s => s.PatientName == "EMMA MILLSTEIN"), $"{vm.Suggestions.Count}");
        Snapshot("statement-3-patient-suggestions.png");
        Act(() => vm.PickPatientCommand.Execute(vm.Suggestions.First(s => s.PatientCode == "2026-00000001")), 500);
        Check("picking one fills the box and closes the list", vm.PatientId != 0 && !vm.HasSuggestions && vm.PatientText.Contains("2026-00000001"), vm.PatientText);
        Act(() => vm.ClearDatesCommand.Execute(null), 200);
        Act(() => vm.PreviewCommand.Execute(null), 2500);
        Check("the preview lists her 11 registrations with paid and balance", vm.Lines.Count == 11 && vm.Summary.Contains("2,890.00") && vm.Summary.Contains("1,250.00"), $"{vm.Lines.Count} {vm.Summary}");
        Snapshot("statement-4-patient-preview.png");
        Act(() => vm.GenerateCommand.Execute(null), 4000);
        Check("the patient statement prints on one page", CapturePreview.LastDocument is { Pages.Count: 1 } && vm.Saved.Count == 2, $"{CapturePreview.LastDocument?.Pages.Count} pages, {vm.Saved.Count} saved");

        // ---- a big company statement over several pages
        Act(() => vm.IsCompanyMode = true, 300);
        Act(() => vm.CompanyId = vm.Companies.First(c => c.Name == "Big Company Inc.").Id, 1500);
        Act(() => vm.Attention = string.Empty, 200);
        CapturePreview.Clear();
        Act(() => vm.PreviewCommand.Execute(null), 3000);
        Check("the whole company is 90 lines", vm.Lines.Count == 90, $"{vm.Lines.Count}");
        Act(() => vm.GenerateCommand.Execute(null), 6000);
        Check("a big statement continues over several pages", CapturePreview.LastDocument is { Pages.Count: > 1 }, $"{CapturePreview.LastDocument?.Pages.Count}");
        Console.WriteLine($"big statement pages: {CapturePreview.LastDocument?.Pages.Count}");

        // ---- the saved ones
        Act(() => vm.SelectedTab = 1, 800);
        Check("the saved list has the three statements, newest first", vm.Saved.Count == 3 && vm.Saved[0].Name.StartsWith("Big Company"), string.Join(" | ", vm.Saved.Select(s => $"{s.Number} {s.Name}")));
        Check("nothing is selected, so no action is on", !vm.ReprintCommand.CanExecute(null) && !vm.VoidCommand.CanExecute(null) && !vm.RegenerateCommand.CanExecute(null), "");
        Act(() => vm.Selected = vm.Saved[2], 300);
        Check("a selection turns the actions on", vm.ReprintCommand.CanExecute(null) && vm.VoidCommand.CanExecute(null) && vm.RegenerateCommand.CanExecute(null), "");
        Snapshot("statement-5-saved.png");

        CapturePreview.Clear();
        Act(() => vm.ReprintCommand.Execute(null), 3000);
        Check("Reprint prints it again, one page", CapturePreview.LastDocument is { Pages.Count: 1 }, "");

        // Void: the statement stays, marked VOID, and prints with VOID across it.
        var number = vm.Selected!.Number;
        Act(() => vm.VoidCommand.Execute(null), 2500);
        Check("Void marks it in the list", vm.Saved.First(s => s.Number == number).IsVoid && vm.Saved.First(s => s.Number == number).StatusText == "VOID", vm.SavedMessage);
        Act(() => vm.Selected = vm.Saved.First(s => s.Number == number), 300);
        Check("a voided statement can not be voided again", !vm.VoidCommand.CanExecute(null) && vm.ReprintCommand.CanExecute(null), "");
        Snapshot("statement-6-voided.png");
        CapturePreview.Clear();
        Act(() => vm.ReprintCommand.Execute(null), 3000);
        Check("a voided statement still reprints", CapturePreview.LastDocument is { Pages.Count: 1 }, "");

        // Regenerate: a new statement from the registrations as they are now.
        Act(() => vm.Selected = vm.Saved.First(s => s.Number == number), 300);
        CapturePreview.Clear();
        Act(() => vm.RegenerateCommand.Execute(null), 4000);
        Check("Regenerate makes a new statement and prints it", vm.Saved.Count == 4 && CapturePreview.LastDocument is { Pages.Count: 1 } && vm.SavedMessage.Contains("Saved as SOA-"), vm.SavedMessage);

        Act(() => vm.SearchText = "Millstein", 300);
        Act(() => vm.SearchSavedCommand.Execute(null), 1500);
        Check("the list can be searched by patient", vm.Saved.Count == 1 && vm.Saved[0].TypeText == "Patient", string.Join(",", vm.Saved.Select(s => s.Name)));
        Act(() => vm.SearchText = string.Empty, 300);
        Act(() => vm.SearchSavedCommand.Execute(null), 1500);
    }
}
