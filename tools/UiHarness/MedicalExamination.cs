using System.Windows;
using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Wpf.ViewModels;

namespace UiHarness;

internal static partial class Program
{
    private static void MedicalExaminationChecks(IServiceProvider provider)
    {
        CapturePreview.OutputFolder = _out;
        _main.WindowState = WindowState.Normal;
        _main.Width = 1366;
        _main.Height = 728;
        _main.Left = 0;
        _main.Top = 0;
        Pump(800);

        var vm = Open<MedicalExaminationViewModel>(ModuleIds.AnnualPhysicalExamPage2);
        Pump(1000);
        Check("seven rows and the lists", vm.Rows.Count == 7 && vm.AssessmentDoneBy.EditCommand is not null, "");
        Snapshot("mer-1-new.png");

        Act(() => vm.FindText = "Emma", 1500);
        var pick = vm.Suggestions.FirstOrDefault();
        if (pick is not null)
            Act(() => vm.PickSuggestionCommand.Execute(pick), 1500);
        Check("a registration fills the patient block", vm.HasRegistration && vm.PatientName.Length > 0, vm.PatientName);

        vm.ContactNo = "34555";
        vm.CivilStatus = "Single";
        string[] marks = ["N", "F", "N", "F", "N", "F", "N"];
        for (var i = 0; i < 7; i++)
        {
            vm.Rows[i].State.Value = marks[i];
            vm.Rows[i].Remarks = i == 0 ? "NOTE X" : null;
        }

        vm.Classification.Value = "Acceptable";
        vm.MedicalSurgicalHistory = "QQQ HISTORY";
        vm.Assessment = "SSS ASSESSMENT";
        vm.Remarks = "WWW REMARKS";
        vm.AssessmentDoneBy.Value = "DR. FIORA";
        vm.PhysicianName.Value = "DR. MALONZO";
        vm.PhysicianLicense.Value = "W2234O4504";
        Pump(400);
        Snapshot("mer-2-filled.png");
        _main.Height = 1700;
        Pump(700);
        Snapshot("mer-2-tall.png");
        _main.Height = 728;
        Pump(300);

        Act(() => vm.SaveCommand.Execute(null), 3000);
        Check("saved", vm.Id != 0 && !vm.IsError, vm.Message);
        var id = vm.Id;

        Act(() => vm.NewRecordCommand.Execute(null), 800);
        Check("new form is empty", vm.Rows.All(r => r.State.Value is null) && vm.Classification.Value is null && vm.AssessmentDoneBy.Value is null, "");
        Act(() => vm.SearchText = "Emma");
        Act(() => vm.SearchCommand.Execute(null));
        Act(() => vm.SelectedItem = vm.Items.First(i => i.Id == id), 1800);
        Check("reopening restores the fields", vm.Rows.Select(r => r.State.Value).SequenceEqual(marks) && vm.Rows[0].Remarks == "NOTE X" && vm.Classification.Value == "Acceptable"
            && vm.MedicalSurgicalHistory == "QQQ HISTORY" && vm.Remarks == "WWW REMARKS" && vm.PhysicianLicense.Value == "W2234O4504", $"{vm.Classification.Value}/{vm.PhysicianName.Value}");

        Act(() => vm.PrintCommand.Execute(null), 3000);
        Check("print opens the layout", CapturePreview.Last is { Layout: DiagnosticLabs.Application.LabResults.ReportLayout.MedicalExamination }, CapturePreview.Last?.Layout.ToString() ?? "none");

        Act(() => vm.NewRecordCommand.Execute(null), 800);
        Act(() => vm.EnterDefaultsCommand.Execute(null), 800);
        vm.Rows[0].State.Value = "N";
        vm.Classification.Value = "Fit";
        vm.Assessment = "DEFAULT ASSESSMENT";
        Act(() => vm.SaveDefaultsCommand.Execute(null), 2500);
        Check("defaults cover marks, classification and texts", vm.Rows[0].State.Value == "N" && vm.Classification.Value == "Fit" && vm.Assessment == "DEFAULT ASSESSMENT", "");
        Act(() => vm.EnterDefaultsCommand.Execute(null), 800);
        Act(() => vm.ClearDefaultsCommand.Execute(null), 2500);

        Act(() => vm.SelectedItem = vm.Items.FirstOrDefault(i => i.Id == id) ?? vm.Items.First(), 1800);
        Act(() => vm.DeleteCommand.Execute(null), 2500);
        Check("deleted", !vm.IsError && vm.Id == 0, vm.Message);
    }
}
