using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Wpf.ViewModels;

namespace UiHarness;

internal static partial class Program
{
    private static void MicroscopyChecks()
    {
        CapturePreview.OutputFolder = _out;
        _main.WindowState = System.Windows.WindowState.Normal;
        _main.Width = 1366;
        _main.Height = 728;
        _main.Left = 0;
        _main.Top = 0;
        Pump(1000);

        var vm = Open<ClinicalMicroscopyViewModel>(ModuleIds.ClinicalMicroscopy);
        Pump(1500);
        var rows = vm.Sections.SelectMany(s => s.Rows).ToList();
        string Ref(string name) => rows.First(r => r.Test.Name == name).Reference.Value ?? "";
        Check("a new form starts with the reference ranges and empty results", Ref("PH") == "5.0 - 8.0" && Ref("SpecificGravity") == "1.005 - 1.030" && Ref("Protein") == "NEGATIVE" && Ref("Ketones") == "NEGATIVE"
            && Ref("WBC") == "0-5/hpf" && Ref("RBC") == "0-2/hpf" && Ref("Crystals") == "hpf" && Ref("Casts") == "lpf" && rows.All(r => r.Result is null), $"{Ref("PH")}/{Ref("WBC")}");
        Check("it starts clean", !vm.IsDirty, "");
        Snapshot("cm-1-new.png");

        Act(() =>
        {
            vm.PatientName = "Rosa Dimaano";
            vm.Age = "41 years old";
            vm.Sex = "Female";
            vm.CompanyOrPhysician = "ACME";
            vm.Remarks = "Repeat in one week";
            vm.MedicalTechnologist = "Lianne Payla T. Dizon, RMT";
            vm.MedicalTechnologist2 = "Ann Jeanette D. Evaristo, RMT";
            vm.Pathologist = "Ma. Angelita C. Gutierrez, MD, FPSP";
            var values = new Dictionary<string, string>
            {
                ["Color"] = "YELLOW", ["Transparency"] = "CLEAR", ["PH"] = "6.0", ["SpecificGravity"] = "1.015", ["Protein"] = "NEGATIVE", ["Glucose"] = "NEGATIVE", ["Blood"] = "NEGATIVE",
                ["Leukocytes"] = "TRACE", ["Nitrite"] = "NEGATIVE", ["Bilirubin"] = "NEGATIVE", ["Urobilinogen"] = "NORMAL", ["Ketones"] = "NEGATIVE", ["WBC"] = "2-4/hpf", ["RBC"] = "0-1/hpf",
                ["MucusThreads"] = "RARE", ["EpithelialCells"] = "FEW", ["AmorphousUratesPhosphates"] = "NONE", ["Bacteria"] = "FEW", ["Crystals"] = "NONE", ["Casts"] = "NONE",
            };
            foreach (var r in rows)
                r.Result = values[r.Test.Name];
        }, 800);
        Check("typing makes it changed", vm.IsDirty, "");
        Snapshot("cm-2-filled.png");
        foreach (var sv in Descendants<System.Windows.Controls.ScrollViewer>(_main))
            sv.ScrollToVerticalOffset(330);
        Pump(400);
        Snapshot("cm-3-scrolled.png");
        foreach (var sv in Descendants<System.Windows.Controls.ScrollViewer>(_main))
            sv.ScrollToEnd();
        Pump(400);
        Snapshot("cm-4-bottom.png");
        foreach (var sv in Descendants<System.Windows.Controls.ScrollViewer>(_main))
            sv.ScrollToHome();

        Act(() => vm.SaveCommand.Execute(null), 2500);
        Check("it saves", vm.Id != 0 && !vm.IsError && !vm.IsDirty, vm.Message);
        Act(() => vm.NewRecordCommand.Execute(null), 800);
        Act(() => vm.ToggleSearchCommand.Execute(null));
        Act(() => vm.SearchCommand.Execute(null), 1500);
        Act(() => vm.SelectedItem = vm.Items.First(), 1800);
        var reopened = vm.Sections.SelectMany(s => s.Rows).ToDictionary(r => r.Test.Name, r => r.Result);
        Check("it reopens with every result and the reference ranges", reopened["Leukocytes"] == "TRACE" && reopened["PH"] == "6.0" && reopened["Casts"] == "NONE" && Ref("WBC") == "0-5/hpf"
            && vm.MedicalTechnologist2 == "Ann Jeanette D. Evaristo, RMT" && vm.Remarks == "Repeat in one week" && !vm.IsDirty, $"{reopened["Leukocytes"]}/{vm.IsDirty}");
        Act(() => vm.ToggleSearchCommand.Execute(null));
        Act(() => vm.PrintCommand.Execute(null), 3000);
        Check("print opens the Clinical Microscopy page", CapturePreview.Last is { Layout: DiagnosticLabs.Application.LabResults.ReportLayout.ClinicalMicroscopy } r && r.Signatories.Count == 3, CapturePreview.Last?.Layout.ToString() ?? "none");
    }
}
