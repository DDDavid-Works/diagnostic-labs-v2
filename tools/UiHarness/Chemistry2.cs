using System.Windows;
using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Wpf.ViewModels;

namespace UiHarness;

internal static partial class Program
{
    private static void Chemistry2Checks(IServiceProvider provider)
    {
        CapturePreview.OutputFolder = _out;
        _main.WindowState = WindowState.Normal;
        _main.Width = 1366;
        _main.Height = 728;
        _main.Left = 0;
        _main.Top = 0;
        Pump(800);

        var vm = Open<ClinicalChemistry2ViewModel>(ModuleIds.ClinicalChemistry2);
        Pump(1000);
        Snapshot("chem2-1-new.png");

        Act(() => vm.FindText = "Emma", 1500);
        var pick = vm.Suggestions.FirstOrDefault();
        if (pick is not null)
            Act(() => vm.PickSuggestionCommand.Execute(pick), 1500);
        Check("a registration fills the patient block", vm.HasRegistration && vm.PatientName.Length > 0, vm.PatientName);

        vm.AlkalinePhosphatase.Show(new("APCNV", "APCU", "10"), new("APSNV", "APSU", "11"));
        vm.ASTSGOT.Show(new("SGOTCNV", "SGOTCU", "12"), new("SGOTSNV", "SGOTSU", "13"));
        vm.MedicalTechnologist = "DR. MICHAUD";
        vm.Pathologist = "DR. FIORA";
        Pump(400);
        Snapshot("chem2-2-filled.png");
        Act(() => vm.SaveCommand.Execute(null), 3000);
        Check("saved", vm.Id != 0 && !vm.IsError, vm.Message);
        var id = vm.Id;

        Act(() => vm.NewRecordCommand.Execute(null), 800);
        Check("new form is empty", vm.AlkalinePhosphatase.ConventionalResult is null && vm.ASTSGOT.SystemUnit is null, "");
        Act(() => vm.SearchText = "Emma");
        Act(() => vm.SearchCommand.Execute(null));
        Act(() => vm.SelectedItem = vm.Items.First(i => i.Id == id), 1800);
        Check("reopening restores every cell", vm.AlkalinePhosphatase.ConventionalNormalValue == "APCNV" && vm.AlkalinePhosphatase.SystemResult == "11" && vm.ASTSGOT.ConventionalUnit == "SGOTCU" && vm.ASTSGOT.SystemResult == "13", "");

        Act(() => vm.PrintCommand.Execute(null), 3000);
        Check("print opens the layout", CapturePreview.Last is { Layout: DiagnosticLabs.Application.LabResults.ReportLayout.ClinicalChemistry2 }, CapturePreview.Last?.Layout.ToString() ?? "none");

        Act(() => vm.NewRecordCommand.Execute(null), 800);
        Act(() => vm.EnterDefaultsCommand.Execute(null), 800);
        vm.AlkalinePhosphatase.ConventionalNormalValue = "DEFNV";
        vm.ASTSGOT.SystemUnit = "DEFU";
        Act(() => vm.SaveDefaultsCommand.Execute(null), 2500);
        Check("defaults keep the normal values and units", vm.AlkalinePhosphatase.ConventionalNormalValue == "DEFNV" && vm.ASTSGOT.SystemUnit == "DEFU", "");
        Act(() => vm.EnterDefaultsCommand.Execute(null), 800);
        Act(() => vm.ClearDefaultsCommand.Execute(null), 2500);

        Act(() => vm.SelectedItem = vm.Items.FirstOrDefault(i => i.Id == id) ?? vm.Items.First(), 1800);
        Act(() => vm.DeleteCommand.Execute(null), 2500);
        Check("deleted", !vm.IsError && vm.Id == 0, vm.Message);
    }
}
