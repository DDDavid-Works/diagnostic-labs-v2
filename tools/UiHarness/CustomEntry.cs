using System.IO;
using System.Windows.Controls;
using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.LabResults.CustomForms;
using DiagnosticLabs.Wpf.Printing;
using DiagnosticLabs.Wpf.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace UiHarness;

internal static partial class Program
{
    // The entry screen of the form the designer check made ("Lipid Profile": group HEMATOLOGY with a heading and a reference column, group CHEMISTRY without
    // one, three text boxes, two technologists and a pathologist, licence numbers off): fill it, save, reopen, defaults, entry lists, an older design, print.
    private static void CustomEntryChecks(IServiceProvider provider, DiagnosticLabs.Application.LabResults.CustomForms.FormSummary form)
    {
        var moduleId = form.ModuleId;
        CapturePreview.OutputFolder = _out;

        // Presses a button and waits until the screen is not busy any more.
        void Press(CommunityToolkit.Mvvm.Input.IRelayCommand command, CustomFormViewModel screen) =>
            ActUntil(() => command.Execute(null), () => !screen.IsBusy, 15000);

        var entry = Open<CustomFormViewModel>(moduleId);
        Pump(2000);
        Check("the entry screen is titled with the form's name", entry.Title == "Lipid Profile", entry.Title);
        Check("it has the two groups of the design", entry.Groups.Count == 2 && entry.Groups[0].Title == "HEMATOLOGY" && entry.Groups[1].Title == "CHEMISTRY" && !entry.Groups[1].ShowReference,
            string.Join(", ", entry.Groups.Select(g => g.Title)));
        var hematology = entry.Groups[0].Rows;
        Check("the heading row has nothing to enter and the tests have results and references",
            hematology.Count == 5 && hematology[2].IsSection && hematology[2].Choice is null && hematology.Where(r => r.IsTest).All(r => r.Choice is not null && r.Reference is not null && r.ShowReferenceCell)
            && !entry.Groups[1].Rows[0].ShowReferenceCell,
            string.Join(", ", hematology.Select(r => r.Label)));
        Check("the text boxes and the signatories follow the design",
            entry.Textboxes.Select(t => t.Label).SequenceEqual(["Findings", "Impression", "Advice"]) && entry.ShowFirstTechnologist && entry.ShowSecondTechnologist && entry.ShowPathologist
            && !entry.ShowLicenses && entry.ShowRemarks && !entry.HasDesignNote,
            $"first={entry.ShowFirstTechnologist} second={entry.ShowSecondTechnologist} path={entry.ShowPathologist} lic={entry.ShowLicenses}");
        Snapshot("custom-1-empty.png");

        // an entry list for one row: edited through the Entry Builder and offered in that row's dropdown only
        var hemoglobin = hematology[0];
        Check("a row's dropdown starts with no choices", hemoglobin.Choice!.Items.Count == 0, $"{hemoglobin.Choice.Items.Count}");
        ActUntil(() => hemoglobin.Choice.EditCommand!.Execute(null), () => !entry.IsBusy && hemoglobin.Choice.Items.Count > 0, 15000);
        Check("editing its list adds the choice to that row only", hemoglobin.Choice.Items.SequenceEqual(["Harness option"]) && hematology[1].Choice!.Items.Count == 0, string.Join(", ", hemoglobin.Choice.Items));
        Check("the row's list is shown under the row's name, not its id", hemoglobin.Choice.Entry.Title == "Hemoglobin" && hemoglobin.Choice.Entry.Name.Length == 32, $"{hemoglobin.Choice.Entry.Title} / {hemoglobin.Choice.Entry.Name}");

        // fill and save
        // set inside Act: typing a patient name starts a background lookup that must run on the dispatcher
        Act(() =>
        {
            entry.PatientName = "Custom Patient";
            entry.PatientCode = "CP-1";
            entry.Age = "41";
            entry.Sex = "Male";
        }, 600);
        entry.MedicalTechnologist = "Tech One";
        entry.MedicalTechnologist2 = "Tech Two";
        entry.Pathologist = "Dr Path";
        hemoglobin.Choice.Value = "Harness option";
        hemoglobin.Reference!.Value = "12-16 g/dL";
        hematology[1].Choice!.Value = "42";
        hematology[3].Choice!.Value = "30";
        entry.Groups[1].Rows[0].Choice!.Value = "95";
        entry.Textboxes[0].Text = "No abnormal findings.";
        Snapshot("custom-2-filled.png");
        ScrollForm(_main, 100000);
        Snapshot("custom-2b-bottom.png");
        ScrollForm(_main, 0);
        Check("a changed form is dirty", entry.IsDirty, "");
        Press(entry.SaveCommand, entry);
        Check("the result is saved", !entry.IsError && entry.Message.StartsWith("Saved") && entry.Id != 0 && !entry.IsDirty, entry.Message);
        var savedId = entry.Id;

        // reopen it: start a new one, then pick the saved one from the list
        Press(entry.NewRecordCommand, entry);
        Check("a new result starts empty", entry.IsNew && entry.PatientName.Length == 0 && hemoglobin.Choice.Value is null && entry.Textboxes[0].Text is null, "");
        Press(entry.SearchCommand, entry);
        Act(() => entry.SelectedItem = entry.Items.First(i => i.PatientName == "Custom Patient"), 100);
        ActUntil(() => { }, () => !entry.IsBusy && entry.Id == savedId);
        Check("reopening shows every value", entry.Id == savedId && entry.Groups[0].Rows[0].Choice!.Value == "Harness option" && entry.Groups[0].Rows[0].Reference!.Value == "12-16 g/dL"
            && entry.Groups[0].Rows[1].Choice!.Value == "42" && entry.Groups[0].Rows[3].Choice!.Value == "30" && entry.Groups[1].Rows[0].Choice!.Value == "95"
            && entry.Textboxes[0].Text == "No abnormal findings." && entry.MedicalTechnologist2 == "Tech Two", $"{entry.Groups[0].Rows[0].Choice!.Value}");
        Check("nothing is changed right after opening", !entry.IsDirty, "");
        Snapshot("custom-3-reopened.png");
        entry.Groups[0].Rows[0].Reference!.Value = "changed";
        Check("changing a reference value makes it dirty", entry.IsDirty, "");
        entry.Groups[0].Rows[0].Reference!.Value = "12-16 g/dL";

        // defaults: reference values and texts a new result starts with
        Press(entry.EnterDefaultsCommand, entry);
        Check("the defaults mode shows a new form", entry.IsSettingDefaults && entry.IsNew, "");
        entry.Groups[0].Rows[1].Reference!.Value = "36-48 %";
        entry.Textboxes[1].Text = "Advised to repeat in 6 months.";
        Press(entry.SaveDefaultsCommand, entry);
        Check("the defaults are saved", !entry.IsError && !entry.IsSettingDefaults && entry.Message.StartsWith("Defaults saved"), entry.Message);
        Check("a new result starts with the defaults", entry.IsNew && entry.Groups[0].Rows[1].Reference!.Value == "36-48 %" && entry.Textboxes[1].Text == "Advised to repeat in 6 months."
            && entry.Groups[0].Rows[1].Choice!.Value is null, $"{entry.Groups[0].Rows[1].Reference!.Value}");

        // the printout of the saved result: opened again from the list, then Print (it saves first, then shows the preview)
        Press(entry.NewRecordCommand, entry);
        Act(() => entry.SelectedItem = entry.Items.First(i => i.PatientName == "Custom Patient"), 100);
        ActUntil(() => { }, () => !entry.IsBusy && entry.Id == savedId);
        CapturePreview.Clear();
        Press(entry.PrintCommand, entry);
        var printed = CapturePreview.Last;
        Check("print opens the preview of the saved result", !entry.IsError && printed is { Layout: DiagnosticLabs.Application.LabResults.ReportLayout.CustomForm, Custom: not null } && printed.Title == "Lipid Profile", entry.Message);
        if (printed is not null)
        {
            File.Copy(Path.Combine(_out, "print-CustomForm.png"), Path.Combine(_out, "custom-print-1.png"), true);
            using var scope = provider.CreateScope();
            var design = scope.ServiceProvider.GetRequiredService<IFormDesignerService>().GetAsync(form.Id).GetAwaiter().GetResult().Value.Definition;
            var bottom = CustomFormLayout.Draw(new Canvas(), printed);
            Check("the page ends where the designer's estimate says it does", Math.Abs(bottom - CustomFormPage.EstimateBottom(design)) < 0.01, $"drawn {bottom:N1}, estimated {CustomFormPage.EstimateBottom(design):N1}");
            var texts = File.ReadAllText(Path.Combine(_out, "print-CustomForm-text.txt"));
            Check("the printout carries the header cells, rows, values, text box and signatories", texts.Contains("Custom Patient") && texts.Contains("HEMATOLOGY") && texts.Contains("12-16 g/dL") && texts.Contains("Harness option")
                && texts.Contains("DIFFERENTIAL COUNT") && texts.Contains("No abnormal findings.") && texts.Contains("Tech Two") && texts.Contains("PATHOLOGIST") && !texts.Contains("LICENSE NO"), "");
        }

        Press(entry.NewRecordCommand, entry);

        // the form gets a new design: Hemoglobin is renamed and the CHEMISTRY group goes
        var designer = Open<FormDesignerViewModel>(ModuleIds.FormDesigner);
        Pump(1500);
        ActUntil(() => designer.SelectedForm = designer.Forms.First(f => f.Name == "Lipid Profile"), () => !designer.IsBusy && designer.FormName == "Lipid Profile");
        designer.Groups[0].Rows[0].Name = "Hgb";
        designer.RemoveGroupCommand.Execute(designer.Groups[1]);
        designer.Message = string.Empty;
        ActUntil(() => designer.SaveCommand.Execute(null), () => !designer.IsBusy && designer.Message.Length > 0);
        Check("the new design is saved as version 2", designer.Message == "Saved as version 2.", designer.Message);

        var again = Open<CustomFormViewModel>(moduleId);
        Pump(2000);
        Check("a new screen shows the newest design", again.Groups.Count == 1 && again.Groups[0].Rows[0].Label == "Hgb" && !again.HasDesignNote, $"{again.Groups.Count} groups");
        Snapshot("custom-4-version2.png");
        Press(again.SearchCommand, again);
        Act(() => again.SelectedItem = again.Items.First(i => i.PatientName == "Custom Patient"), 100);
        ActUntil(() => { }, () => !again.IsBusy && again.Id == savedId);
        Pump(800);
        Check("the old result is shown with the design it was saved with and says so", again.Id == savedId && again.Groups.Count == 2 && again.Groups[0].Rows[0].Label == "Hemoglobin"
            && again.HasDesignNote && again.DesignNote.Contains("version 1") && again.Groups[1].Rows[0].Choice!.Value == "95", $"{again.Groups.Count} groups, note: {again.DesignNote}");
        Snapshot("custom-5-old-design.png");
        Press(again.NewRecordCommand, again);
        Check("a new result goes back to the newest design", again.IsNew && again.Groups.Count == 1 && again.Groups[0].Rows[0].Label == "Hgb" && !again.HasDesignNote, $"{again.Groups.Count} groups");

        // the one-group design with long values: a value that has to shrink to its cell, a text box and Remarks that grow down
        Act(() => again.PatientName = "Long Values Patient", 600);
        again.Remarks = string.Join(" ", Enumerable.Repeat("Remarks that run on over several lines of the box.", 6));
        again.Groups[0].Rows[0].Choice!.Value = "13.5";
        again.Groups[0].Rows[0].Reference!.Value = "12.0 - 16.0 g/dL (adult female)";
        again.Groups[0].Rows[1].Choice!.Value = "POSITIVE (+++) WITH MANY CRYSTALS AND CASTS SEEN";
        again.Groups[0].Rows[3].Choice!.Value = "30";
        again.Textboxes[0].Text = string.Join(" ", Enumerable.Repeat("The findings of the pathologist run over several lines of the text box.", 5));
        CapturePreview.Clear();
        Press(again.PrintCommand, again);
        Check("a result with long values prints", !again.IsError && CapturePreview.Last is { Custom: not null }, again.Message);
        File.Copy(Path.Combine(_out, "print-CustomForm.png"), Path.Combine(_out, "custom-print-2.png"), true);
        using (var scope2 = provider.CreateScope())
        {
            var design2 = scope2.ServiceProvider.GetRequiredService<IFormDesignerService>().GetAsync(form.Id).GetAwaiter().GetResult().Value.Definition;
            var drawn = CustomFormLayout.Draw(new Canvas(), CapturePreview.Last!);
            Check("long texts grow the page below the one-line estimate", drawn > CustomFormPage.EstimateBottom(design2) + 20, $"drawn {drawn:N1}, estimated {CustomFormPage.EstimateBottom(design2):N1}, limit {CustomFormPage.BottomLimit:N0}");
        }
    }
}
