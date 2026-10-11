using System.IO;
using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Wpf.ViewModels;

namespace UiHarness;

internal static partial class Program
{
    // Opens the four screens the client renamed fields on, fills those fields, saves, reopens and prints (the printouts are written as PNGs).
    private static void RenameChecks()
    {
        CapturePreview.OutputFolder = _out;
        _main.WindowState = System.Windows.WindowState.Normal;
        _main.Width = 1366;
        _main.Height = 728;
        _main.Left = 0;
        _main.Top = 0;
        Pump(1000);

        var uri = Open<UrinalysisViewModel>(ModuleIds.Urinalysis);
        Pump(1200);
        Act(() => { uri.PatientName = "Emma Millstein"; uri.Reaction.Value = "6.0"; uri.Protein.Value = "TRACE"; uri.Glucose.Value = "NEGATIVE"; }, 500);
        Snapshot("rename-urinalysis.png");
        Act(() => uri.SaveCommand.Execute(null), 2500);
        Check("urinalysis saves with Protein and Glucose", uri.Id != 0 && !uri.IsError, uri.Message);
        Act(() => uri.PrintCommand.Execute(null), 3000);
        Check("its printout is labelled Reaction (PH), Protein, Glucose", CapturePreview.Last is { } u && u.ResultLines.Select(l => l.Label).Contains("Reaction (PH)") && u.ResultLines.Select(l => l.Label).Contains("Protein") && u.ResultLines.Select(l => l.Label).Contains("Glucose") && !u.ResultLines.Select(l => l.Label).Contains("Albumin"), "");
        Check("the builder heading of Reaction names it Reaction (PH)", uri.Reaction.Entry.Name == "Reaction (PH)" && uri.Protein.Entry.Name == "Protein" && uri.Glucose.Entry.Name == "Glucose", uri.Reaction.Entry.Name);

        var hema = Open<HematologyViewModel>(ModuleIds.Hematology);
        Pump(1200);
        Act(() =>
        {
            hema.PatientName = "Emma Millstein";
            hema.Remarks = "Repeat in a week";
            hema.MedicalTechnologist = "Andrea Marie P. Gueco, RMT";
            hema.MedicalTechnologist2 = "Patricia Nicole D. Malonzo, RMT";
            hema.Pathologist = "Ma. Angelita C. Gutierrez, MD, FPSP";
            var refs = new Dictionary<string, string>
            {
                ["Hematocrit"] = "MALE: 42.0-52.0%    FEMALE: 37.0-47.0%", ["Hemoglobin"] = "MALE: 140-170 g/L    FEMALE: 120-155 g/L",
                ["RBCCount"] = "3.50-5.50 \u00D710\u00B9\u00B2/L", ["WBCCount"] = "5.0-10.0 \u00D710\u2079/L", ["Neutrophils"] = "50.0-70.0%",
                ["Lymphocytes"] = "10.0-40.0%", ["Eosinophils"] = "0.0-0.05%", ["Monocytes"] = "0.0-0.07%", ["Basophils"] = "0.0-0.01%", ["PlateletCount"] = "150-400 \u00D710\u2079/L",
            };
            var i = 0;
            foreach (var r in hema.Rows)
            {
                i++;
                r.NormalValue = refs[r.Name];
                r.Result = (4 + i).ToString();
            }
        }, 600);
        Snapshot("rename-hematology.png");
        foreach (var sv in Descendants<System.Windows.Controls.ScrollViewer>(_main))
            sv.ScrollToEnd();
        Pump(400);
        Snapshot("rename-hematology-scrolled.png");
        foreach (var sv in Descendants<System.Windows.Controls.ScrollViewer>(_main))
            sv.ScrollToHome();
        Act(() => hema.SaveCommand.Execute(null), 2500);
        Check("hematology saves the new table", hema.Id != 0 && !hema.IsError && hema.Rows.Select(x => x.Label).SequenceEqual(["Hematocrit", "Hemoglobin", "Red Blood Cell Count", "White Blood Cell Count", "Neutrophils", "Lymphocytes", "Eosinophils", "Monocytes", "Basophils", "Platelet Count"]), hema.Message);
        Act(() => hema.NewRecordCommand.Execute(null), 800);
        Act(() => hema.ToggleSearchCommand.Execute(null));
        Act(() => hema.SearchCommand.Execute(null), 1500);
        Act(() => hema.SelectedItem = hema.Items.First(), 1800);
        Check("it reopens with every cell", hema.Rows[2].Result == "7" && hema.Rows[2].NormalValue!.StartsWith("3.50") && hema.MedicalTechnologist2 == "Patricia Nicole D. Malonzo, RMT" && !hema.IsDirty, $"{hema.Rows[2].Result}/{hema.IsDirty}");
        Act(() => hema.ToggleSearchCommand.Execute(null));
        Act(() => hema.PrintCommand.Execute(null), 3000);

        var chem = Open<ClinicalChemistryViewModel>(ModuleIds.ClinicalChemistry);
        Pump(1200);
        Act(() =>
        {
            chem.PatientName = "Emma Millstein";
            chem.Remarks = "Fasting for 8 hours";
            chem.MedicalTechnologist = "Andrea Marie P. Gueco, RMT";
            chem.MedicalTechnologist2 = "Patricia Nicole D. Malonzo, RMT";
            chem.Pathologist = "Ma. Angelita C. Gutierrez, MD, FPSP";
            var i = 0;
            foreach (var r in chem.Rows)
            {
                i++;
                r.ConventionalNormalValue = i % 3 == 0 ? "0.70-1.40" : "60-121";
                r.ConventionalUnit = i > 8 ? "U/L" : "mg/dL";
                r.ConventionalResult = (80 + i).ToString();
                r.SystemNormalValue = "3.34-6.73";
                r.SystemUnit = i > 8 ? "U/L" : "mmol/L";
                r.SystemResult = (4 + i).ToString();
            }
        }, 600);
        Snapshot("rename-chemistry.png");
        foreach (var sv in Descendants<System.Windows.Controls.ScrollViewer>(_main))
            sv.ScrollToEnd();
        Pump(400);
        Snapshot("rename-chemistry-scrolled.png");
        foreach (var sv in Descendants<System.Windows.Controls.ScrollViewer>(_main))
            sv.ScrollToHome();
        Act(() => chem.SaveCommand.Execute(null), 2500);
        Check("clinical chemistry saves the new table", chem.Id != 0 && !chem.IsError && chem.Rows.Select(x => x.Label).SequenceEqual(["Fasting Blood Sugar", "Cholesterol", "Triglycerides", "HDL", "LDL", "Creatinine", "Blood Urea Nitrogen", "Blood Uric Acid", "ALT/SGPT", "AST/SGOT"]), chem.Message);
        Act(() => chem.NewRecordCommand.Execute(null), 800);
        Act(() => chem.ToggleSearchCommand.Execute(null));
        Act(() => chem.SearchCommand.Execute(null), 1500);
        Act(() => chem.SelectedItem = chem.Items.First(), 1800);
        Check("it reopens with every cell", chem.Rows[0].ConventionalResult == "81" && chem.Rows[9].SystemResult == "14" && chem.Rows[9].ConventionalUnit == "U/L" && chem.MedicalTechnologist2 == "Patricia Nicole D. Malonzo, RMT" && !chem.IsDirty, $"{chem.Rows[0].ConventionalResult}/{chem.Rows[9].SystemResult}/{chem.IsDirty}");
        Act(() => chem.ToggleSearchCommand.Execute(null));
        Act(() => chem.PrintCommand.Execute(null), 3000);

        var chem2 = Open<ClinicalChemistry2ViewModel>(ModuleIds.ClinicalChemistry2);
        Pump(1200);
        Act(() => { chem2.PatientName = "Emma Millstein"; chem2.ASTSGOT.ConventionalNormalValue = "5-40"; chem2.ASTSGOT.ConventionalUnit = "U/L"; chem2.ASTSGOT.ConventionalResult = "22"; }, 500);
        Snapshot("rename-chemistry2.png");
        Act(() => chem2.SaveCommand.Execute(null), 2500);
        Check("clinical chemistry 2 saves with AST/SGOT", chem2.Id != 0 && !chem2.IsError && chem2.ASTSGOT.Label == "AST/SGOT", chem2.Message);
        Act(() => chem2.PrintCommand.Execute(null), 3000);
    }
}
