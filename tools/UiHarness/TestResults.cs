using System.Windows;
using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Wpf.ViewModels;

namespace UiHarness;

internal static partial class Program
{
    private static void SerologyChecks(IServiceProvider provider)
    {
        var vm = PrepareTestResult<SerologyViewModel>(ModuleIds.Serology);
        TestResultChecks(vm, vm.Test, () => vm.Result, v => vm.Result = v, "sero");
    }

    private static void PregnancyChecks(IServiceProvider provider)
    {
        var vm = PrepareTestResult<PregnancyTestViewModel>(ModuleIds.PregnancyTest);
        TestResultChecks(vm, vm.Test, () => vm.Result, v => vm.Result = v, "preg", hasTest: false);
    }

    private static void Chemistry1Checks(IServiceProvider provider)
    {
        var vm = PrepareTestResult<ClinicalChemistry1ViewModel>(ModuleIds.ClinicalChemistry1);
        TestResultChecks(vm, vm.Test, () => vm.Result, v => vm.Result = v, "chem1");
    }

    private static void ImmunologyChecks(IServiceProvider provider)
    {
        var vm = PrepareTestResult<ImmunologyViewModel>(ModuleIds.Immunology);
        TestResultChecks(vm, vm.Test, () => vm.Result, v => vm.Result = v, "immu");
    }

    private static T PrepareTestResult<T>(int moduleId) where T : class
    {
        CapturePreview.OutputFolder = _out;
        _main.WindowState = WindowState.Normal;
        _main.Width = 1366;
        _main.Height = 728;
        _main.Left = 0;
        _main.Top = 0;
        Pump(800);
        var vm = Open<T>(moduleId);
        Pump(1000);
        return vm;
    }

    private static void TestResultChecks(dynamic vm, ChoiceField test, Func<string?> getResult, Action<string?> setResult, string prefix, bool hasTest = true)
    {
        Check("the test list has its command", !hasTest || test.EditCommand is not null, "");
        Snapshot($"{prefix}-1-new.png");

        Act(() => vm.FindText = "Emma", 1500);
        var pick = ((IEnumerable<DiagnosticLabs.Application.LabResults.ResultRegistrationMatch>)vm.Suggestions).FirstOrDefault();
        if (pick is not null)
            Act(() => vm.PickSuggestionCommand.Execute(pick), 1500);
        Check("a registration fills the patient block", vm.HasRegistration && ((string)vm.PatientName).Length > 0, (string)vm.PatientName);

        if (hasTest)
            test.Value = "Hepatitis B Screening";
        setResult("POSITIVE");
        vm.Remarks = "GOOD";
        vm.MedicalTechnologist = "DR. MICHAUD";
        vm.Pathologist = "DR. FIORA";
        Pump(400);
        Snapshot($"{prefix}-2-filled.png");
        Act(() => vm.SaveCommand.Execute(null), 3000);
        Check("saved", vm.Id != 0 && !vm.IsError, (string)vm.Message);
        long id = vm.Id;

        Act(() => vm.NewRecordCommand.Execute(null), 800);
        Check("new form is empty", test.Value is null && getResult() is null, "");
        Act(() => vm.SearchText = "Emma");
        Act(() => vm.SearchCommand.Execute(null));
        Act(() => vm.SelectedItem = ((IEnumerable<DiagnosticLabs.Application.LabResults.LabResultListItem>)vm.Items).First(i => i.Id == id), 1800);
        Check("reopening restores the test and result", (!hasTest || test.Value == "Hepatitis B Screening") && getResult() == "POSITIVE", $"{test.Value}/{getResult()}");

        Act(() => vm.PrintCommand.Execute(null), 3000);
        Check("print opens the layout", CapturePreview.Last is { Layout: DiagnosticLabs.Application.LabResults.ReportLayout.TestResult }, CapturePreview.Last?.Layout.ToString() ?? "none");

        Act(() => vm.SelectedItem = ((IEnumerable<DiagnosticLabs.Application.LabResults.LabResultListItem>)vm.Items).FirstOrDefault(i => i.Id == id) ?? ((IEnumerable<DiagnosticLabs.Application.LabResults.LabResultListItem>)vm.Items).First(), 1800);
        Act(() => vm.DeleteCommand.Execute(null), 2500);
        Check("deleted", !vm.IsError && vm.Id == 0, (string)vm.Message);
    }
}
