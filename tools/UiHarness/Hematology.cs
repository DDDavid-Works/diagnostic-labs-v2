using System.Windows;
using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Wpf.ViewModels;

namespace UiHarness;

internal static partial class Program
{
    private static void HematologyChecks(IServiceProvider provider)
    {
        CapturePreview.OutputFolder = _out;
        _main.WindowState = WindowState.Normal;
        _main.Width = 1366;
        _main.Height = 728;
        _main.Left = 0;
        _main.Top = 0;
        Pump(800);

        var vm = Open<HematologyViewModel>(ModuleIds.Hematology);
        Pump(1000);
        Check("ten rows", vm.Rows.Count == 10, "");
        Snapshot("hema-1-new.png");

        Act(() => vm.FindText = "Emma", 1500);
        var pick = vm.Suggestions.FirstOrDefault();
        if (pick is not null)
            Act(() => vm.PickSuggestionCommand.Execute(pick), 1500);
        Check("a registration fills the patient block", vm.HasRegistration && vm.PatientName.Length > 0, vm.PatientName);

        string[] normals = ["MALE: XXX    FEMALE: XXX", "MALE: YYY    FEMALE: YYY", "KKK", "UUU", "HHH", "PPP", "BBB", "FFF", "VVV", "DDD"];
        for (var i = 0; i < 10; i++)
        {
            vm.Rows[i].NormalValue = normals[i];
            vm.Rows[i].Result = $"R{i + 1}";
        }

        vm.Remarks = "REMARKS XXX";
        vm.MedicalTechnologist = "DR. MICHAUD";
        vm.Pathologist = "DR. FIORA";
        Pump(400);
        Snapshot("hema-2-filled.png");
        Act(() => vm.SaveCommand.Execute(null), 3000);
        Check("saved", vm.Id != 0 && !vm.IsError, vm.Message);
        var id = vm.Id;

        Act(() => vm.NewRecordCommand.Execute(null), 800);
        Check("new form is empty", vm.Rows.All(r => r.NormalValue is null && r.Result is null), "");
        Act(() => vm.SearchText = "Emma");
        Act(() => vm.SearchCommand.Execute(null));
        Act(() => vm.SelectedItem = vm.Items.First(i => i.Id == id), 1800);
        Check("reopening restores every cell", vm.Rows.Select(r => r.NormalValue).SequenceEqual(normals) && vm.Rows.Select(r => r.Result).SequenceEqual(Enumerable.Range(1, 10).Select(i => $"R{i}")), string.Join(",", vm.Rows.Select(r => r.Result)));

        Act(() => vm.PrintCommand.Execute(null), 3000);
        Check("print opens the Hematology layout", CapturePreview.Last is { Layout: DiagnosticLabs.Application.LabResults.ReportLayout.Hematology } r && r.Fields is not null, CapturePreview.Last?.Layout.ToString() ?? "none");

        // defaults: the normal values
        Act(() => vm.NewRecordCommand.Execute(null), 800);
        Act(() => vm.EnterDefaultsCommand.Execute(null), 800);
        for (var i = 0; i < 10; i++)
            vm.Rows[i].NormalValue = normals[i];
        Act(() => vm.SaveDefaultsCommand.Execute(null), 2500);
        Check("defaults keep the normal values", vm.Rows.Select(r => r.NormalValue).SequenceEqual(normals), "");
        vm.Rows[0].NormalValue = "changed";
        Act(() => vm.ResetToDefaultsCommand.Execute(null), 300);
        Check("reset restores them", vm.Rows[0].NormalValue == normals[0], vm.Rows[0].NormalValue ?? "");
        Snapshot("hema-3-defaults.png");
        Act(() => vm.EnterDefaultsCommand.Execute(null), 800);
        Act(() => vm.ClearDefaultsCommand.Execute(null), 2500);
        Check("defaults can be cleared", vm.Rows.All(r => r.NormalValue is null), "");

        Act(() => vm.SelectedItem = vm.Items.FirstOrDefault(i => i.Id == id) ?? vm.Items.First(), 1800);
        Act(() => vm.DeleteCommand.Execute(null), 2500);
        Check("deleted", !vm.IsError && vm.Id == 0, vm.Message);
    }
}
