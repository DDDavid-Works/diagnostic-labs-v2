using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Wpf.ViewModels;

namespace UiHarness;

internal static partial class Program
{
    private static void PhysicalChecks()
    {
        CapturePreview.OutputFolder = _out;
        _main.WindowState = System.Windows.WindowState.Normal;
        _main.Width = 1366;
        _main.Height = 728;
        _main.Left = 0;
        _main.Top = 0;
        Pump(1000);

        var vm = Open<PhysicalExaminationViewModel>(ModuleIds.PhysicalExamination);
        Pump(1500);
        Check("a new form starts with the sheet's normal values and empty dates", vm.Cells["HematocritNValue"].Value == "0.42 - 0.52" && vm.Cells["HematocritFemaleNValue"].Value == "0.37 - 0.47"
            && vm.Cells["StabNValue"].Value == "0 - 0.05" && vm.Cells["WBCCountNValue"].Value!.StartsWith("5 - 10") && vm.Dates.Values.All(d => d.Value is null) && vm.Cells["HematocritResult"].Value is null,
            $"{vm.Cells["HematocritNValue"].Value}/{vm.Cells["WBCCountNValue"].Value}");
        Check("it starts clean", !vm.IsDirty, "");
        Snapshot("pe-1-new.png");

        Act(() =>
        {
            vm.PatientName = "Rosa Dimaano";
            vm.Age = "41 years old";
            vm.Sex = "Female";
            vm.CompanyOrPhysician = "ACME";
            vm.Dates["CbcDate"].Value = new DateTime(2026, 10, 10);
            vm.Dates["BloodTypingDate"].Value = new DateTime(2026, 10, 10);
            vm.Dates["UrinalysisDate"].Value = new DateTime(2026, 10, 9);
            vm.Dates["FecalysisDate"].Value = new DateTime(2026, 10, 9);
            foreach (var c in new[] { "Hematocrit", "Hemoglobin", "WBCCount", "Segmenters", "Lymphocytes", "Eosinophils", "Monocytes", "Basophils", "Stab" })
                vm.Cells[c + "Result"].Value = c == "Hemoglobin" ? "132 g/l" : "0.4" + c.Length;
            vm.Choices["BloodTyping"].Value = "O";
            vm.Choices["RhTyping"].Value = "Positive";
            foreach (var (k, v) in new Dictionary<string, string> { ["UrineColor"] = "YELLOW", ["UrineAppearance"] = "CLEAR", ["UrineReaction"] = "6.0", ["UrineSPGravity"] = "1.015", ["UrineAlbumin"] = "NEGATIVE",
                ["UrineSugar"] = "NEGATIVE", ["UrinePusCells"] = "0-2/HPF", ["UrineRedCells"] = "0-1/HPF", ["UrineMucusThreads"] = "RARE", ["UrineEpithelialCells"] = "FEW", ["UrineAmorphousUratesPO4"] = "NONE",
                ["UrineBacteria"] = "FEW", ["UrineCrystals"] = "NONE", ["UrineCasts"] = "NONE", ["FecalysisColor"] = "BROWN", ["FecalysisConsistency"] = "FORMED" })
                vm.Choices[k].Value = v;
            vm.Cells["UrineOthers"].Value = "None seen";
            vm.Cells["FecalysisResult"].Value = "No ova or parasite seen";
            vm.Cells["Others"].Value = "For repeat after one week.";
            vm.MedicalTechnologist = "Andrea Marie P. Gueco, RMT";
            vm.Pathologist = "Ma. Angelita C. Gutierrez, MD, FPSP";
        }, 800);
        Check("typing makes it changed", vm.IsDirty, "");
        Snapshot("pe-2-filled.png");
        foreach (var sv in Descendants<System.Windows.Controls.ScrollViewer>(_main))
            sv.ScrollToVerticalOffset(320);
        Pump(400);
        Snapshot("pe-3-scrolled.png");
        foreach (var sv in Descendants<System.Windows.Controls.ScrollViewer>(_main))
            sv.ScrollToEnd();
        Pump(400);
        Snapshot("pe-4-bottom.png");
        foreach (var sv in Descendants<System.Windows.Controls.ScrollViewer>(_main))
            sv.ScrollToHome();

        Act(() => vm.SaveCommand.Execute(null), 2500);
        Check("it saves", vm.Id != 0 && !vm.IsError && !vm.IsDirty, vm.Message);
        Act(() => vm.NewRecordCommand.Execute(null), 800);
        Act(() => vm.ToggleSearchCommand.Execute(null));
        Act(() => vm.SearchCommand.Execute(null), 1500);
        Act(() => vm.SelectedItem = vm.Items.First(), 1800);
        Check("it reopens with every section", vm.Cells["HemoglobinResult"].Value == "132 g/l" && vm.Choices["RhTyping"].Value == "Positive" && vm.Choices["UrineAmorphousUratesPO4"].Value == "NONE"
            && vm.Cells["FecalysisResult"].Value == "No ova or parasite seen" && vm.Dates["UrinalysisDate"].Value == new DateTime(2026, 10, 9) && vm.Cells["Others"].Value == "For repeat after one week." && !vm.IsDirty,
            $"{vm.Cells["HemoglobinResult"].Value}/{vm.IsDirty}");
        Act(() => vm.ToggleSearchCommand.Execute(null));
        Act(() => vm.PrintCommand.Execute(null), 3000);
        Check("print opens the Physical Examination page", CapturePreview.Last is { Layout: DiagnosticLabs.Application.LabResults.ReportLayout.PhysicalExamination } r && r.Signatories.Count == 2, CapturePreview.Last?.Layout.ToString() ?? "none");
    }
}
