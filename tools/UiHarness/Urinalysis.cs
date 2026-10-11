using System.Windows;
using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Wpf.ViewModels;

namespace UiHarness;

internal static partial class Program
{
    private static void UrinalysisChecks(IServiceProvider provider)
    {
        CapturePreview.OutputFolder = _out;
        _main.WindowState = WindowState.Normal;
        _main.Width = 1366;
        _main.Height = 728;
        _main.Left = 0;
        _main.Top = 0;
        Pump(800);

        var vm = Open<UrinalysisViewModel>(ModuleIds.Urinalysis);
        Pump(1000);
        Check("lists load for every choice field and each has its command", vm.Rows.Count == 8 && vm.Color.EditCommand is not null && vm.Rows.All(r => r.Left.EditCommand is not null), "");
        Check("the table has two columns, the right one shorter", vm.Rows[5].Right is not null && vm.Rows[6].Right is null && vm.Rows[7].Right is null, "");
        Snapshot("urin-1-new.png");

        Act(() => vm.FindText = "Emma", 1500);
        var pick = vm.Suggestions.First();
        Act(() => vm.PickSuggestionCommand.Execute(pick), 1500);
        Check("a registration fills the patient block", vm.HasRegistration && vm.PatientName.Length > 0, vm.PatientName);

        string[] values = ["GREEN", "NORMAL", "1", "2", "3", "4", "5", "6", "7", "8", "9", "A", "B", "C"];
        var fields = new[] { vm.Color, vm.Appearance, vm.Reaction, vm.SPGravity, vm.Protein, vm.Glucose, vm.PusCells, vm.RedCells, vm.MucusThreads, vm.EpithelialCells, vm.AmorphousUratesPO4, vm.Bacteria, vm.Casts, vm.Crystals };
        for (var i = 0; i < fields.Length; i++)
            fields[i].Value = values[i];
        vm.Others = "OTHERS XXXX";
        vm.Remarks = "REMARKS XXX";
        vm.MedicalTechnologist = "DR. MICHAUD";
        vm.Pathologist = "DR. FIORA";
        Pump(400);
        Snapshot("urin-2-filled.png");
        Act(() => vm.SaveCommand.Execute(null), 3000);
        Check("saved", vm.Id != 0 && !vm.IsError, vm.Message);
        var id = vm.Id;

        Act(() => vm.NewRecordCommand.Execute(null), 800);
        Act(() => vm.SearchText = "Emma");
        Act(() => vm.SearchCommand.Execute(null));
        Act(() => vm.SelectedItem = vm.Items.First(i => i.Id == id), 1800);
        Check("reopening restores every field", fields.Select(f => f.Value).SequenceEqual(values) && vm.Others == "OTHERS XXXX", string.Join(",", fields.Select(f => f.Value)));

        Act(() => vm.PrintCommand.Execute(null), 3000);
        Check("print opens the Urinalysis layout", CapturePreview.Last is { Layout: DiagnosticLabs.Application.LabResults.ReportLayout.Urinalysis } r && r.ResultLines.Count == 14, CapturePreview.Last?.Layout.ToString() ?? "none");

        // defaults for list fields
        Act(() => vm.NewRecordCommand.Execute(null), 800);
        Act(() => vm.EnterDefaultsCommand.Execute(null), 800);
        vm.Color.Value = "YELLOW";
        vm.Others = "DEFAULT OTHERS";
        Act(() => vm.SaveDefaultsCommand.Execute(null), 2500);
        Check("defaults cover the list fields and Others", vm.Color.Value == "YELLOW" && vm.Others == "DEFAULT OTHERS" && vm.PatientName.Length == 0, $"{vm.Color.Value} / {vm.Others}");
        vm.Color.Value = "RED";
        Act(() => vm.ResetToDefaultsCommand.Execute(null), 300);
        Check("reset restores them", vm.Color.Value == "YELLOW", vm.Color.Value ?? "");
        Act(() => vm.EnterDefaultsCommand.Execute(null), 800);
        Act(() => vm.ClearDefaultsCommand.Execute(null), 2500);
        Check("defaults can be cleared", vm.Color.Value is null && vm.Others is null, "");

        Act(() => vm.SelectedItem = vm.Items.FirstOrDefault(i => i.Id == id) ?? vm.Items.First(), 1800);
        Act(() => vm.DeleteCommand.Execute(null), 2500);
        Check("deleted", !vm.IsError && vm.Id == 0, vm.Message);
    }
}