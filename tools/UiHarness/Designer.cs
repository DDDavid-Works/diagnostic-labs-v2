using CommunityToolkit.Mvvm.Input;
using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.LabResults.CustomForms;
using DiagnosticLabs.Domain.Catalog;
using DiagnosticLabs.Wpf.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace UiHarness;

internal static partial class Program
{
    // The Custom Lab Form designer: make a form, change the header grid (a field can only be used once), edit the footer, save, reopen, switch off and on.
    // Needs the real Modules (run with -StartAt 20261012104153_StatementsOfAccount so the migration seeds the Custom Lab Form designer module).
    private static void DesignerChecks(IServiceProvider provider)
    {
        _main.WindowState = System.Windows.WindowState.Normal;
        _main.Width = 1366;
        _main.Height = 728;
        _main.Left = 0;
        _main.Top = 0;
        Pump(800);

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
            db.Services.AddRange(
                new Service { ServiceName = "Lipid Profile", ServiceDescription = "d", Price = 100 },
                new Service { ServiceName = "Thyroid Panel", ServiceDescription = "d", Price = 200 },
                new Service { ServiceName = "Vitamin D", ServiceDescription = "d", Price = 300 });
            db.SaveChangesAsync().GetAwaiter().GetResult();
        }

        var menu = string.Join(", ", _mainVm.Menu.SelectMany(g => g.Items).Where(i => i.ModuleId == ModuleIds.FormDesigner).Select(i => i.Name));
        Check("Custom Lab Form is in the menu", menu == "Custom Lab Form", menu);

        var vm = Open<FormDesignerViewModel>(ModuleIds.FormDesigner);
        Pump(1500);

        // Presses a button that ends with a message (saved / refused / switched) and waits for it.
        void Press(IRelayCommand command)
        {
            vm.Message = string.Empty;
            ActUntil(() => command.Execute(null), () => !vm.IsBusy && vm.Message.Length > 0);
        }

        Check("the designer starts on a new form with the default design", vm.IsNew && vm.Cells.Count == 6 && vm.Signatories.Count == 2 && vm.Services.Count == 3, $"{vm.Services.Count} services");
        Snapshot("designer-1-new.png");

        // header: a field already used elsewhere is not offered in the other cells
        var firstCell = vm.Cells[0];
        var secondCell = vm.Cells[1];
        Check("a field used in one cell is not offered in the others",
            firstCell.Selected.Label == "Patient Code" && !secondCell.Options.Any(o => o.Label == "Patient Code") && secondCell.Options.Any(o => o.Label == "Patient Name"),
            string.Join(", ", secondCell.Options.Select(o => o.Label)));
        Act(() => firstCell.Selected = HeaderOption.Empty, 300);
        Check("freeing a cell offers its field to the others again", secondCell.Options.Any(o => o.Label == "Patient Code"), string.Join(", ", secondCell.Options.Select(o => o.Label)));
        Act(() => firstCell.Selected = HeaderOption.For(HeaderField.Age), 300);
        Check("a field another cell uses can not be picked", firstCell.Selected == HeaderOption.Empty && vm.Cells[3].Selected.Label == "Age", firstCell.Selected.Label);
        Act(() => vm.Cells[3].Selected = HeaderOption.Empty, 300);
        Act(() => firstCell.Selected = HeaderOption.For(HeaderField.Age), 300);
        Check("moving a field: Age is now in cell 1 and offered nowhere else", firstCell.Selected.Label == "Age" && vm.Cells.Skip(1).All(c => !c.Options.Any(o => o.Label == "Age")), firstCell.Selected.Label);

        Press(vm.SaveCommand);
        Check("a new form needs a name and a service", vm.IsError && vm.Message.Contains("name"), vm.Message);

        vm.FormName = "Lipid Profile";
        vm.SelectedService = vm.Services.First(s => s.Name == "Lipid Profile");
        Act(() => vm.AddSignatoryCommand.Execute(null), 300);
        Check("a third signatory can be added, a fourth cannot", vm.Signatories.Count == 3 && !vm.CanAddSignatory, $"{vm.Signatories.Count}");
        Act(() => vm.Signatories[1].Role = RoleOption.All[0], 300); // technologist, technologist, technologist
        Press(vm.SaveCommand);
        Check("three technologists are refused", vm.IsError && vm.Message.Contains("medical technologists"), vm.Message);
        Act(() => vm.Signatories[2].Role = RoleOption.All[1], 300); // technologist, technologist, pathologist
        vm.ShowLicenseNumber = false;

        // body: a group of tests and headings, rows reordered, limits, a second group without the reference column, text boxes
        var left = vm.Groups[0];
        Check("a new form has one group", vm.Groups.Count == 1 && left.Label == "Group 1" && left.ShowReference && left.Rows.Count == 0, $"{vm.Groups.Count}");
        Act(() => left.Title = "HEMATOLOGY", 100);
        string[] names = ["Hemoglobin", "Hematocrit", "DIFFERENTIAL COUNT", "Neutrophils", "Lymphocytes"];
        for (var i = 0; i < names.Length; i++)
        {
            Act(() => (i == 2 ? left.AddSectionCommand : left.AddRowCommand).Execute(null), 50);
            left.Rows[i].Name = names[i];
        }

        Check("rows and a heading were added in order", left.Rows.Count == 5 && left.Rows[2].IsSection && !left.Rows[0].IsSection && left.RowCountText == "5 of 30 rows", left.RowCountText);
        var neutrophils = left.Rows[3];
        Act(() => left.MoveRow(neutrophils, left.Rows[0]), 100); // drop Neutrophils on the first row: it goes to the top
        Check("a row dragged onto another takes its place", left.Rows[0] == neutrophils && left.Rows.Select(r => r.Name).SequenceEqual(["Neutrophils", "Hemoglobin", "Hematocrit", "DIFFERENTIAL COUNT", "Lymphocytes"]), string.Join(", ", left.Rows.Select(r => r.Name)));
        Act(() => left.MoveRow(neutrophils, left.Rows[4]), 100); // and back down to the bottom
        Check("and to the bottom again", left.Rows[4] == neutrophils, string.Join(", ", left.Rows.Select(r => r.Name)));
        Act(() => left.AddRowCommand.Execute(null), 50);
        Press(vm.SaveCommand);
        Check("an empty row name is refused", vm.IsError && vm.Message.Contains("needs a test name"), vm.Message);
        Act(() => left.RemoveRowCommand.Execute(left.Rows[^1]), 50);
        Check("a row can be removed", left.Rows.Count == 5, $"{left.Rows.Count}");

        while (left.Rows.Count < 30)
        {
            left.AddRowCommand.Execute(null);
            left.Rows[^1].Name = $"Test {left.Rows.Count}";
        }

        Check("a group stops at 30 rows", left.Rows.Count == 30 && !left.AddRowCommand.CanExecute(null) && !left.AddSectionCommand.CanExecute(null) && left.RowCountText == "30 of 30 rows", left.RowCountText);
        Check("30 rows with Remarks and signatories do not fit one page, and the warning says how much", vm.HasFitWarning && vm.FitWarning.Contains("rows too long"), vm.FitWarning);
        Snapshot("designer-2c-warning.png");
        ScrollForm(_main, 700);
        Snapshot("designer-2a-thirty-rows.png");
        ScrollForm(_main, 0);
        while (left.Rows.Count > 5)
            left.RemoveRowCommand.Execute(left.Rows[^1]);

        Check("with 5 rows the warning is gone", !vm.HasFitWarning, vm.FitWarning);

        Act(() => vm.AddGroupCommand.Execute(null), 100);
        var right = vm.Groups[1];
        Check("a second group can be added, a third cannot", vm.Groups.Count == 2 && right.Label == "Group 2" && !vm.CanAddGroup, $"{vm.Groups.Count}");
        right.Title = "CHEMISTRY";
        right.ShowReference = false;
        right.AddRowCommand.Execute(null);
        right.Rows[0].Name = "Glucose";
        for (var i = 0; i < 3; i++)
        {
            vm.AddTextboxCommand.Execute(null);
            vm.Textboxes[i].Label = new[] { "Findings", "Impression", "Advice" }[i];
        }

        Check("a fourth text box is not allowed", vm.Textboxes.Count == 3 && !vm.CanAddTextbox, $"{vm.Textboxes.Count}");
        ScrollForm(_main, 440);
        Snapshot("designer-2-filled.png");
        ScrollForm(_main, 100000);
        Snapshot("designer-2b-filled-end.png");
        ScrollForm(_main, 0);
        var bodyIds = left.Rows.Select(r => r.Id).ToList();
        Press(vm.SaveCommand);
        Check("the form is created", !vm.IsError && !vm.IsNew && vm.Forms.Count == 1 && vm.Message.StartsWith("The form was created"), vm.Message);
        Check("the saved body keeps its rows, their ids and their order", vm.Groups.Count == 2 && vm.Groups[0].Rows.Select(r => r.Id).SequenceEqual(bodyIds)
            && vm.Groups[0].Rows[4].Name == "Neutrophils" && vm.Groups[0].Rows[2].IsSection && !vm.Groups[1].ShowReference && vm.Textboxes.Count == 3, string.Join(", ", vm.Groups[0].Rows.Select(r => r.Name)));
        Check("its service stays with it and is not offered to another form", vm.SelectedService?.Name == "Lipid Profile" && vm.Services.Count(s => s.Name == "Lipid Profile") == 1, string.Join(", ", vm.Services.Select(s => s.Name)));
        Snapshot("designer-3-saved.png");
        ScrollForm(_main, 600);
        Snapshot("designer-3b-footer.png");
        ScrollForm(_main, 0);

        // a second form
        ActUntil(() => vm.NewFormCommand.Execute(null), () => !vm.IsBusy && vm.IsNew);
        Check("a new form starts blank and the first form's service is taken", vm.IsNew && vm.FormName.Length == 0 && vm.SelectedService is null && !vm.Services.Any(s => s.Name == "Lipid Profile"), string.Join(", ", vm.Services.Select(s => s.Name)));
        vm.FormName = "Thyroid Panel";
        vm.SelectedService = vm.Services.First(s => s.Name == "Thyroid Panel");
        Press(vm.SaveCommand);
        Check("the second form is created", !vm.IsError && vm.Forms.Count == 2, vm.Message);

        // reopen the first one: the design comes back as it was saved
        ActUntil(() => vm.SelectedForm = vm.Forms.First(f => f.Name == "Lipid Profile"), () => !vm.IsBusy && vm.FormName == "Lipid Profile");
        Check("reopening shows the saved design", vm.FormName == "Lipid Profile" && !vm.ShowLicenseNumber && vm.Signatories.Count == 3 && vm.Cells[0].Selected.Label == "Age",
            $"{vm.FormName} lic={vm.ShowLicenseNumber} sig={vm.Signatories.Count} cell1={vm.Cells[0].Selected.Label}");
        Check("nothing is changed right after opening", !vm.IsDirty, "");
        Check("reopening shows the body as it was saved", vm.Groups.Count == 2 && vm.Groups[0].Title == "HEMATOLOGY" && vm.Groups[0].Rows.Select(r => r.Id).SequenceEqual(bodyIds)
            && vm.Groups[1].Rows.Single().Name == "Glucose" && vm.Textboxes.Select(t => t.Label).SequenceEqual(["Findings", "Impression", "Advice"]), string.Join(", ", vm.Groups.Select(g => g.Title)));
        ScrollForm(_main, 440);
        Snapshot("designer-3c-reopened.png");
        ScrollForm(_main, 0);
        var firstName = vm.Groups[0].Rows[0].Name;
        vm.Groups[0].Rows[0].Name = firstName + " (renamed)";
        Check("renaming a row makes the form dirty and keeps the row id", vm.IsDirty && vm.Groups[0].Rows[0].Id == bodyIds[0], "");
        vm.Groups[0].Rows[0].Name = firstName;
        Check("renaming back makes it clean again", !vm.IsDirty, "");

        // unsaved changes are asked about when moving to another form
        vm.FormName = "Lipid Profile Plus";
        Check("a change makes the form dirty", vm.IsDirty, "");
        AutoConfirm.Asked = 0;
        AutoConfirm.NextChoice = DiagnosticLabs.Wpf.Services.SaveChoice.Cancel;
        ActUntil(() => vm.SelectedForm = vm.Forms.First(f => f.Name == "Thyroid Panel"), () => AutoConfirm.Asked == 1 && !vm.IsBusy);
        Check("Cancel keeps you on the form with its changes", AutoConfirm.Asked == 1 && vm.FormName == "Lipid Profile Plus" && vm.SelectedForm?.Name == "Lipid Profile", $"asked {AutoConfirm.Asked}, {vm.FormName}, list on {vm.SelectedForm?.Name}");
        AutoConfirm.NextChoice = DiagnosticLabs.Wpf.Services.SaveChoice.Discard;
        ActUntil(() => vm.SelectedForm = vm.Forms.First(f => f.Name == "Thyroid Panel"), () => !vm.IsBusy && vm.FormName == "Thyroid Panel");
        Check("Don't save moves on without the change", vm.FormName == "Thyroid Panel" && !vm.IsDirty, vm.FormName);

        // a changed design becomes version 2; an unchanged one does not
        Press(vm.SaveCommand);
        Check("saving without a change keeps version 1", vm.Message == "Saved." && vm.Heading.Contains("version 1"), $"{vm.Message} / {vm.Heading}");
        vm.ShowRemarks = false;
        Press(vm.SaveCommand);
        Check("a changed design is saved as version 2", vm.Message == "Saved as version 2." && vm.Heading.Contains("version 2"), $"{vm.Message} / {vm.Heading}");

        // switch off and on again
        Press(vm.ToggleActiveCommand);
        Check("switching off hides the form and frees its service", vm.IsSwitchedOff && !vm.CanEdit && vm.Services.Any(s => s.Name == "Thyroid Panel"), vm.Message);
        Snapshot("designer-4-off.png");
        vm.SelectedService = null;
        Press(vm.ToggleActiveCommand);
        Check("switching on without a service is refused", vm.IsError, vm.Message);
        vm.SelectedService = vm.Services.First(s => s.Name == "Vitamin D");
        Press(vm.ToggleActiveCommand);
        Check("switching on with a free service works", vm.IsActiveForm && !vm.IsError && vm.SelectedService?.Name == "Vitamin D", vm.Message);
        Snapshot("designer-5-on.png");

        // the designer refreshes the menu by itself, so the forms are there without signing in again
        ActUntil(() => { }, () => _mainVm.Menu.SelectMany(g => g.Items).Count(i => i.Name is "Lipid Profile" or "Thyroid Panel") == 2);
        // the scratch database copies the modules of the real one, so forms the client already made may be in the menu too; only this check's two are looked for
        var forms = _mainVm.Menu.SelectMany(g => g.Items).Where(i => i.ModuleId >= ModuleIds.FirstCustomForm && i.Name is "Lipid Profile" or "Thyroid Panel").Select(i => i.Name).OrderBy(n => n).ToList();
        Check("both forms are in the menu without signing in again", forms.SequenceEqual(["Lipid Profile", "Thyroid Panel"]), string.Join(", ", forms));

        CustomEntryChecks(provider, vm.Forms.First(f => f.Name == "Lipid Profile"));
    }

    // Runs an action and waits until the screen has finished (the condition holds) instead of guessing a delay.
    private static void ActUntil(Action action, Func<bool> done, int timeoutMs = 12000)
    {
        Act(action, 50);
        var watch = System.Diagnostics.Stopwatch.StartNew();
        while (!done() && watch.ElapsedMilliseconds < timeoutMs)
            Pump(50);

        Pump(150);
    }
}
