using System.Windows;
using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Domain.Catalog;
using DiagnosticLabs.Domain.Patients;
using DiagnosticLabs.Domain.Registrations;
using DiagnosticLabs.Wpf.ViewModels;
using DiagnosticLabs.Wpf.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace UiHarness;

internal static partial class Program
{
    // Complete Blood Count, Urinalysis and Stool/Fecalysis as sections of the Physical Examination sheet, from the home chips: CBC opens a new sheet
    // (without the urinalysis and fecalysis defaults), Urinalysis asks where and goes on that sheet (taking its defaults), Fecalysis goes on its own
    // form. Run without -StartAt: the check adds the modules, services and links it needs (the migration is proved by the tests and on DiagnosticLabsV2).
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

            var registration = new PatientRegistration
            {
                RegistrationCode = "SEC-0001", Patient = new Patient { PatientCode = "SEC-P1", PatientName = "SECTION SAMPLE", Sex = "Female" },
                InputDate = DateTime.UtcNow, AmountDue = 330m,
            };
            foreach (var service in new[] { cbc, urine, stool })
                registration.Services.Add(new PatientRegistrationService { Service = service, Price = service.Price });
            db.PatientRegistrations.Add(registration);
            db.SaveChangesAsync().GetAwaiter().GetResult();
        }

        // The choice window on its own
        var choice = new FormChoiceWindow("Where do you want to enter the Urinalysis result?", "Urinalysis", ["Urinalysis", "Physical Examination sheet"], 1)
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

        // 1. CBC: a new sheet; the urinalysis and fecalysis defaults are left out, the normal values and signatory stay
        Act(() => home.OpenChipCommand.Execute(cbcChip), 3500);
        var sheet = _mainVm.CurrentPage as PhysicalExaminationViewModel;
        Check("the CBC chip opened a new Physical Examination sheet on the registration", sheet is { Id: 0, HasRegistration: true }, _mainVm.CurrentPage.GetType().Name);
        if (sheet is null)
            return;

        Check("the sections the registration does not put on the sheet start blank",
            sheet.Choices["UrineColor"].Value is null && sheet.Choices["UrineReaction"].Value is null && sheet.Choices["FecalysisColor"].Value is null,
            $"{sheet.Choices["UrineColor"].Value}/{sheet.Choices["FecalysisColor"].Value}");
        Check("the blood count normal values and the signatory still come from the defaults",
            sheet.Cells["HematocritNValue"].Value == "0.42 - 0.52" && sheet.MedicalTechnologist == "HARNESS TECH", sheet.Cells["HematocritNValue"].Value ?? "-");
        Act(() =>
        {
            sheet.Dates["CbcDate"].Value = DateTime.Today;
            sheet.Cells["HematocritResult"].Value = "0.45";
            sheet.Cells["HemoglobinResult"].Value = "142 g/l";
            sheet.Cells["WBCCountResult"].Value = "7.2";
        }, 500);
        Act(() => sheet.SaveCommand.Execute(null), 3000);
        Check("the sheet with the blood count saves", sheet.Id > 0, sheet.Message);
        var sheetId = sheet.Id;

        home = Home();
        cbcChip = Chip(home, "Complete Blood Count");
        urineChip = Chip(home, "Urinalysis");
        Check("the CBC chip is ticked once the blood count has results", cbcChip.IsMade && cbcChip.LatestResultId == sheetId, cbcChip.ToolTip);
        Check("Urinalysis is still to do (the sheet does not have it)", !urineChip.IsMade && urineChip.Choices.Count == 2, urineChip.ToolTip);
        Snapshot("sections-2-home-cbc-done.png");

        // 2. Urinalysis: asked where; the sheet is suggested (it exists) and opens with the urinalysis defaults
        AutoConfirm.NextOption = null;
        var asked = AutoConfirm.Chosen;
        Act(() => home.OpenChipCommand.Execute(urineChip), 3500);
        Check("Urinalysis asked where to enter it", AutoConfirm.Chosen == asked + 1, $"{AutoConfirm.Chosen}");
        sheet = _mainVm.CurrentPage as PhysicalExaminationViewModel;
        Check("choosing the sheet opens the same sheet", sheet?.Id == sheetId, $"{sheet?.Id} vs {sheetId}");
        if (sheet is null)
            return;

        Check("the urinalysis section takes its defaults; fecalysis stays blank",
            sheet.Choices["UrineColor"].Value == "LIGHT YELLOW" && sheet.Choices["FecalysisColor"].Value is null && sheet.Cells["HematocritResult"].Value == "0.45",
            $"{sheet.Choices["UrineColor"].Value}/{sheet.Choices["FecalysisColor"].Value}");
        Act(() =>
        {
            sheet.Dates["UrinalysisDate"].Value = DateTime.Today;
            sheet.Choices["UrineAppearance"].Value = "CLEAR";
            sheet.Choices["UrinePusCells"].Value = "0-2/hpf";
            sheet.Choices["UrineSPGravity"].Value = "1.020";
        }, 500);
        Act(() => sheet.SaveCommand.Execute(null), 3000);
        Check("the sheet saves with the urinalysis", !sheet.IsError, sheet.Message);
        Snapshot("sections-3-sheet.png");
        Act(() => sheet.PrintCommand.Execute(null), 3000);

        home = Home();
        urineChip = Chip(home, "Urinalysis");
        var stoolChip = Chip(home, "Stool/Fecalysis");
        Check("the Urinalysis chip is ticked and opens the sheet", urineChip.IsMade && urineChip.ModuleId == ModuleIds.PhysicalExamination && urineChip.LatestResultId == sheetId, urineChip.ToolTip);
        Check("Fecalysis still asks", stoolChip.Choices.Count == 2 && !stoolChip.IsMade, stoolChip.ToolTip);

        // 3. Fecalysis on its own form; the choice is kept on the registration
        AutoConfirm.NextOption = 0;
        Act(() => home.OpenChipCommand.Execute(stoolChip), 3500);
        AutoConfirm.NextOption = null;
        Check("choosing its own form opens Stool/Fecalysis on the registration", _mainVm.CurrentPage is StoolFecalysisViewModel { HasRegistration: true, Id: 0 }, _mainVm.CurrentPage.GetType().Name);

        home = Home();
        stoolChip = Chip(home, "Stool/Fecalysis");
        Check("the kept choice: the Fecalysis chip now opens its own form without asking", stoolChip.ModuleId == ModuleIds.StoolFecalysis && stoolChip.Choices.Count == 0, stoolChip.ToolTip);
        Snapshot("sections-4-home.png");

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
            var lines = db.PatientRegistrationServices.AsNoTracking().Include(s => s.Service).ToList();
            Check("the registration keeps where each result goes",
                lines.Single(s => s.Service.ServiceName == "Urinalysis").ResultModuleId == ModuleIds.PhysicalExamination
                && lines.Single(s => s.Service.ServiceName == "Stool/Fecalysis").ResultModuleId == ModuleIds.StoolFecalysis
                && lines.Single(s => s.Service.ServiceName == "Complete Blood Count").ResultModuleId is null,
                string.Join(", ", lines.Select(l => $"{l.Service.ServiceName}={l.ResultModuleId}")));
        }
    }
}
