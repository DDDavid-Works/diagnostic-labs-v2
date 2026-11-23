using System.Windows;
using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Domain.Catalog;
using DiagnosticLabs.Domain.Patients;
using DiagnosticLabs.Domain.Registrations;
using DiagnosticLabs.Wpf.ViewModels;
using DiagnosticLabs.Wpf.Views;
using Microsoft.Extensions.DependencyInjection;

namespace UiHarness;

internal static partial class Program
{
    // Complete Blood Count, Urinalysis and Stool/Fecalysis as sections of the Physical Examination sheet, from the home chips: Urinalysis asks where;
    // picking its own form and leaving without saving keeps nothing (it asks again); picking the sheet opens a new sheet with the urinalysis defaults
    // but not the fecalysis ones; once saved, CBC and Urinalysis open that sheet, and Fecalysis on the sheet takes its defaults there.
    // Run without -StartAt: the check adds the modules, services and links it needs (the migration is proved by the tests and on DiagnosticLabsV2).
    private static void SectionChecks(IServiceProvider provider)
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
            var stool = new Service { ServiceName = "Stool/Fecalysis", ServiceDescription = "d", Price = 90m, IsActive = true };
            var urine = new Service { ServiceName = "Urinalysis", ServiceDescription = "d", Price = 90m, IsActive = true };
            var cbc = new Service { ServiceName = "Complete Blood Count", ServiceDescription = "d", Price = 150m, IsActive = true };
            db.Services.AddRange(stool, urine, cbc);
            db.SaveChangesAsync().GetAwaiter().GetResult();
            var type = new DiagnosticLabs.Domain.Identity.ModuleType { Id = 3, ModuleTypeName = "Lab Results", Icon = "folder", SortOrder = 1, IsActive = true };
            db.ModuleTypes.Add(type);
            foreach (var (id, name, service) in new[] { (ModuleIds.StoolFecalysis, "Stool/Fecalysis", (long?)stool.Id), (ModuleIds.Urinalysis, "Urinalysis", urine.Id), (ModuleIds.PhysicalExamination, "Physical Examination", null) })
            {
                db.Modules.Add(new DiagnosticLabs.Domain.Identity.Module
                {
                    Id = id, ModuleType = type, ModuleName = name, HasView = true, HasCreate = true, HasEdit = true, HasPrint = true, HasSearch = true,
                    HasShowList = true, HasSetDefaults = true, IsActive = true, ServiceId = service,
                });
            }

            db.ModuleDefaults.Add(new DiagnosticLabs.Domain.Settings.ModuleDefault
            {
                ModuleId = ModuleIds.PhysicalExamination, IsActive = true,
                Defaults = """{"UrineColor":"LIGHT YELLOW","UrineReaction":"6.0","FecalysisColor":"BROWN","HematocritNValue":"0.42 - 0.52","MedicalTechnologist":"HARNESS TECH"}""",
            });
            db.SaveChangesAsync().GetAwaiter().GetResult();
            db.ServiceFormSections.AddRange(
                new ServiceFormSection { ServiceId = cbc.Id, ModuleId = ModuleIds.PhysicalExamination, Section = FormSection.Cbc },
                new ServiceFormSection { ServiceId = urine.Id, ModuleId = ModuleIds.PhysicalExamination, Section = FormSection.Urinalysis },
                new ServiceFormSection { ServiceId = stool.Id, ModuleId = ModuleIds.PhysicalExamination, Section = FormSection.Fecalysis });

            db.Modules.Add(new DiagnosticLabs.Domain.Identity.Module { Id = ModuleIds.BatchPrint, ModuleType = type, ModuleName = "Batch Print", HasView = true, HasPrint = true, IsActive = true });
            var registration = new PatientRegistration
            {
                RegistrationCode = "SEC-0001", Patient = new Patient { PatientCode = "SEC-P1", PatientName = "SECTION SAMPLE", Sex = "Female" },
                InputDate = DateTime.UtcNow, AmountDue = 330m, Company = new Company { CompanyName = "Section Company" }, BatchName = "Batch A",
            };
            foreach (var service in new[] { cbc, urine, stool })
                registration.Services.Add(new PatientRegistrationService { Service = service, Price = service.Price });
            db.PatientRegistrations.Add(registration);
            db.SaveChangesAsync().GetAwaiter().GetResult();
        }

        // The choice window on its own
        // (with the longest labels: a result saved on the sheet, none on the form yet)
        var choice = new FormChoiceWindow("The Stool/Fecalysis result is saved. Open it, or enter one in the other form as well?", "Stool/Fecalysis",
            [new("Stool/Fecalysis", false, "Enter a result here"), new("Physical Examination sheet", true, "Open the saved result")], 1)
        {
            WindowStartupLocation = WindowStartupLocation.Manual, Left = 0, Top = 0, ShowInTaskbar = false, ShowActivated = false,
        };
        choice.Show();
        Pump(500);
        Snapshot(choice, "sections-0-choice.png");
        choice.Close();

        HomeViewModel Home()
        {
            Act(() => _mainVm.GoHomeCommand.Execute(null), 2500);
            var home = (HomeViewModel)_mainVm.CurrentPage;
            Act(() => home.AllDaysCommand.Execute(null), 1800);
            return home;
        }

        HomeChip Chip(HomeViewModel home, string name) => home.Rows.Single(r => r.PatientName == "SECTION SAMPLE").Chips.Single(c => c.Name == name);

        var home = Home();
        Snapshot("sections-1-home.png");
        var cbcChip = Chip(home, "Complete Blood Count");
        var urineChip = Chip(home, "Urinalysis");
        Check("the CBC chip opens the CBC section of the sheet", cbcChip.CanOpen && cbcChip.ModuleId == ModuleIds.PhysicalExamination && cbcChip.Section == FormSection.Cbc && !cbcChip.IsMade, cbcChip.ToolTip);
        Check("the Urinalysis chip offers its own form and the sheet", urineChip.CanOpen && urineChip.ModuleId is null && urineChip.Choices.Count == 2, urineChip.ToolTip);

        // 1. Urinalysis: pick its own form, leave without saving; the chip must ask again (it used to keep the choice)
        AutoConfirm.NextOption = 0;
        var asked = AutoConfirm.Chosen;
        Act(() => home.OpenChipCommand.Execute(urineChip), 3500);
        Check("picking its own form opens Urinalysis on the registration", _mainVm.CurrentPage is UrinalysisViewModel { HasRegistration: true, Id: 0 }, _mainVm.CurrentPage.GetType().Name);
        home = Home();
        urineChip = Chip(home, "Urinalysis");
        Check("left without saving: the Urinalysis chip still asks", urineChip.ModuleId is null && urineChip.Choices.Count == 2 && !urineChip.IsMade, urineChip.ToolTip);
        Act(() => home.OpenChipCommand.Execute(urineChip), 3500);
        Check("clicking it again asks again", AutoConfirm.Chosen == asked + 2, $"{AutoConfirm.Chosen - asked}");
        home = Home();

        // 2. Urinalysis on the sheet: a new sheet that keeps the urinalysis defaults but not the fecalysis ones
        AutoConfirm.NextOption = 1;
        urineChip = Chip(home, "Urinalysis");
        Act(() => home.OpenChipCommand.Execute(urineChip), 3500);
        AutoConfirm.NextOption = null;
        var sheet = _mainVm.CurrentPage as PhysicalExaminationViewModel;
        Check("picking the sheet opens a new Physical Examination sheet on the registration", sheet is { Id: 0, HasRegistration: true }, _mainVm.CurrentPage.GetType().Name);
        if (sheet is null)
            return;

        Check("the urinalysis section keeps its defaults; fecalysis starts blank",
            sheet.Choices["UrineColor"].Value == "LIGHT YELLOW" && sheet.Choices["UrineReaction"].Value == "6.0" && sheet.Choices["FecalysisColor"].Value is null,
            $"{sheet.Choices["UrineColor"].Value}/{sheet.Choices["FecalysisColor"].Value}");
        Check("the blood count normal values and the signatory still come from the defaults",
            sheet.Cells["HematocritNValue"].Value == "0.42 - 0.52" && sheet.MedicalTechnologist == "HARNESS TECH", sheet.Cells["HematocritNValue"].Value ?? "-");
        Act(() =>
        {
            sheet.Dates["CbcDate"].Value = DateTime.Today;
            sheet.Cells["HematocritResult"].Value = "0.45";
            sheet.Cells["HemoglobinResult"].Value = "142 g/l";
            sheet.Cells["WBCCountResult"].Value = "7.2";
            sheet.Dates["UrinalysisDate"].Value = DateTime.Today;
            sheet.Choices["UrineAppearance"].Value = "CLEAR";
            sheet.Choices["UrinePusCells"].Value = "0-2/hpf";
            sheet.Choices["UrineSPGravity"].Value = "1.020";
        }, 500);
        Act(() => sheet.SaveCommand.Execute(null), 3000);
        Check("the sheet saves with the blood count and the urinalysis", sheet.Id > 0, sheet.Message);
        var sheetId = sheet.Id;
        Snapshot("sections-3-sheet.png");
        Act(() => sheet.PrintCommand.Execute(null), 3000);

        home = Home();
        cbcChip = Chip(home, "Complete Blood Count");
        urineChip = Chip(home, "Urinalysis");
        var stoolChip = Chip(home, "Stool/Fecalysis");
        Check("the CBC chip is ticked and opens the sheet", cbcChip.IsMade && cbcChip.LatestResultId == sheetId, cbcChip.ToolTip);
        Check("the Urinalysis chip is ticked and still offers both forms", urineChip.IsMade && urineChip.LatestResultId == sheetId && urineChip.Choices.Count == 2, urineChip.ToolTip);
        Check("Fecalysis still asks", stoolChip.Choices.Count == 2 && !stoolChip.IsMade, stoolChip.ToolTip);
        Snapshot("sections-2-home.png");

        // 2b. Urinalysis after it is saved: the suggested answer opens the saved result; its own form starts a second record, saved after the question
        asked = AutoConfirm.Chosen;
        Act(() => home.OpenChipCommand.Execute(urineChip), 3500);
        Check("a saved Urinalysis still asks, and the suggested answer opens the saved sheet",
            AutoConfirm.Chosen == asked + 1 && _mainVm.CurrentPage is PhysicalExaminationViewModel { Id: var openedId } && openedId == sheetId, _mainVm.CurrentPage.GetType().Name);
        home = Home();
        AutoConfirm.NextOption = 0;
        Act(() => home.OpenChipCommand.Execute(Chip(home, "Urinalysis")), 3500);
        AutoConfirm.NextOption = null;
        var own = _mainVm.CurrentPage as UrinalysisViewModel;
        Check("its own form opens a new Urinalysis on the registration", own is { Id: 0, HasRegistration: true }, _mainVm.CurrentPage.GetType().Name);
        if (own is not null)
        {
            Act(() => own.Color.Value = "YELLOW", 300);
            Act(() => own.SaveCommand.Execute(null), 3000);
            Check("the second record saves (after the \"already on the sheet\" question)", own.Id > 0, own.Message);
        }

        home = Home();
        urineChip = Chip(home, "Urinalysis");
        Check("Urinalysis counts both records and offers each", urineChip.Count == 2 && urineChip.Choices.All(c => c.ResultId is not null), urineChip.Caption);

        // 3. Fecalysis on the sheet that exists (the suggested answer): the section takes its defaults
        Act(() => home.OpenChipCommand.Execute(stoolChip), 3500);
        sheet = _mainVm.CurrentPage as PhysicalExaminationViewModel;
        Check("Fecalysis on the sheet opens the same sheet with the fecalysis defaults",
            sheet?.Id == sheetId && sheet.Choices["FecalysisColor"].Value == "BROWN" && sheet.Choices["UrinePusCells"].Value == "0-2/hpf",
            $"{sheet?.Id} {sheet?.Choices["FecalysisColor"].Value}");
        home = Home();
        stoolChip = Chip(home, "Stool/Fecalysis");
        Check("not saved: the Fecalysis chip still asks", stoolChip.Choices.Count == 2 && !stoolChip.IsMade, stoolChip.ToolTip);
        Snapshot("sections-4-home.png");

        // 4. Batch Print of Urinalysis, which this patient has on both forms: the latest by default, or one form only
        var batch = Open<BatchPrintViewModel>(ModuleIds.BatchPrint);
        Pump(1000);
        Act(() => batch.CompanyId = batch.Companies.First(x => x.Name == "Section Company").Id, 1500);
        Act(() => batch.ServiceId = batch.Services.First(s => s.Name == "Urinalysis").ServiceId, 500);
        Check("Batch Print offers a Form choice for Urinalysis", batch.HasFormChoice && batch.Forms.Select(f => f.Name).SequenceEqual([BatchPrintViewModel.AnyForm, "Urinalysis", "Physical Examination sheet"]),
            string.Join(", ", batch.Forms.Select(f => f.Name)));
        Act(() => batch.SearchCommand.Execute(null), 2500);
        Check("the latest of either is the Urinalysis form (saved last), and the grid says so", batch.Rows.Single().FormText == "Urinalysis", batch.Rows.FirstOrDefault()?.FormText ?? batch.Message);
        Act(() => batch.FormModuleId = ModuleIds.PhysicalExamination, 300);
        Act(() => batch.SearchCommand.Execute(null), 2500);
        Check("asking for the sheet finds the sheet", batch.Rows.Single().FormText == "Physical Examination sheet" && batch.Rows.Single().ResultId == sheetId, batch.Rows.FirstOrDefault()?.FormText ?? batch.Message);
        Snapshot("sections-5-batch.png");
        Act(() => batch.PrintCommand.Execute(null), 3000);
        Act(() => batch.ServiceId = batch.Services.First(s => s.Name == "Complete Blood Count").ServiceId, 500);
        Check("Complete Blood Count has one form: no Form choice", !batch.HasFormChoice && batch.Forms.Count == 0, $"{batch.Forms.Count}");
    }
}