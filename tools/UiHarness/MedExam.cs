using Microsoft.Extensions.DependencyInjection;
using System.IO;
using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.LabResults;
using DiagnosticLabs.Wpf.Services;
using DiagnosticLabs.Wpf.ViewModels;

namespace UiHarness;

internal static partial class Program
{
    private static void SelectTab(int index) => Act(() =>
    {
        foreach (var tabs in Descendants<System.Windows.Controls.TabControl>(_main))
            tabs.SelectedIndex = index;

        foreach (var sv in Descendants<System.Windows.Controls.ScrollViewer>(_main))
            sv.ScrollToHome();
    }, 600);

    private static void ScrollAll(double? offset)
    {
        foreach (var sv in Descendants<System.Windows.Controls.ScrollViewer>(_main))
        {
            if (offset is { } o)
                sv.ScrollToVerticalOffset(o);
            else
                sv.ScrollToHome();
        }

        Pump(400);
    }

    private static void FillMedExam(MedicalExamReportViewModel vm, bool longValues)
    {
        vm.PatientName = longValues ? "Maria Cristina Dela Cruz-Villanueva y Santos" : "Rosa Dimaano";
        vm.Age = "41 years old";
        vm.Sex = "Female";
        vm.CompanyOrPhysician = longValues ? "BIO R9L Incorporated Manufacturing and Trading Company of the Philippines" : "ACME";
        var form = vm.Form;
        var i = 0;
        foreach (var group in form.Marks.Values)
            group.Value = group.Options[i++ % group.Options.Count].Text;

        // a few rows are left empty on purpose
        form.Marks["HxStd"].Value = null;
        form.Marks["RosTattoo"].Value = null;

        var values = new Dictionary<string, string>
        {
            ["CivilStatus"] = "Single", ["ContactNo"] = "0917 123 4567",
            ["HxOthers"] = longValues ? "Asthma as a child; allergic to shrimp and penicillin; wears braces; had dengue in 2019; occasional migraine when tired; takes vitamins daily." : "Allergic to shrimp",
            ["HxMedication"] = longValues ? "Salbutamol inhaler as needed, ferrous sulfate once daily, mefenamic acid for menstrual pain" : "Salbutamol PRN",
            ["FamOthers"] = "Stroke (father)", ["Lmp"] = "Sep 28, 2026", ["NumPregnancies"] = "2", ["NumMiscarriages"] = "0", ["ObOthers"] = "None",
            ["SmokingSince"] = "2015", ["SticksPerDay"] = "5", ["DrinkingSince"] = "2018", ["Bottles"] = "2",
            ["Bp1"] = "120/80", ["Bp2"] = "118/78", ["Pulse1"] = "72", ["Pulse2"] = "70", ["Temperature"] = "36.6", ["Height"] = "165 cm", ["Weight"] = "58 kg",
            ["BmiCategory"] = longValues ? "Normal (BMI 21.3) - within the healthy range for the applicant's age and height" : "Normal (21.3)",
            ["VisionFarWithR"] = "20/20", ["VisionFarWithL"] = "20/20", ["VisionFarWithoutR"] = "20/30", ["VisionFarWithoutL"] = "20/25",
            ["Ishihara"] = "Normal color vision", ["RosOthersNote"] = "Mild scoliosis",
            ["Findings"] = longValues
                ? "Mild scoliosis of the thoracolumbar spine, no pain on movement. Dental caries on two molars, advised dental consult. Slightly enlarged tonsils without redness. Heart and lungs clear, abdomen soft and non-tender. Old surgical scar on the right lower quadrant (appendectomy)."
                : "Mild scoliosis. Dental caries.",
            ["LabChestXrayNote"] = "Clear lung fields", ["LabCbcNote"] = longValues ? "Slightly low hemoglobin, advised iron supplementation and a repeat CBC in three months" : "WNL",
            ["LabUrinalysisNote"] = "Few epithelial cells", ["LabFecalysisNote"] = "No ova or parasite seen",
            ["Assessment"] = longValues
                ? "Physically fit for employment. Mild scoliosis and dental caries noted, neither of which limits the work applied for. Advised dental consultation and a repeat complete blood count within three months. No restrictions on lifting or prolonged standing. Recommend annual physical examination."
                : "Physically fit for employment.",
        };
        foreach (var (name, value) in values)
            form.Cells[name].Value = value;

        form.Marks["Classification"].Value = "B";
        vm.Remarks = longValues ? "Applicant to present the dental clearance on the next annual medical examination. Results released to the HR department on October 16, 2026." : "For HR.";
        string[] people = ["Nurse Joy Ramos", "Andrea Marie P. Gueco, RMT", "Dr. Carlo Mendoza", "Dr. Romeo G. Malonzo"];
        for (var s = 0; s < form.Signatories.Count; s++)
        {
            form.Signatories[s].Person = people[s];
            form.Signatories[s].License = "L-" + (1000 + s);
        }

        vm.Pathologist = "Dr. Romeo G. Malonzo";
        vm.PathologistLicense = "87844";
    }

    // The three forms that ask for Civil Status share the list the Patients screen keeps in the Entry Builder.
    private static void CivilStatusChecks(IServiceProvider provider)
    {
        CapturePreview.OutputFolder = _out;
        _main.WindowState = System.Windows.WindowState.Normal;
        _main.Width = 1366;
        _main.Height = 728;
        Pump(1000);
        var patients = Open<PatientsViewModel>(ModuleIds.Patients);
        var expected = patients.CivilStatuses.ToList();
        var ape = Open<AnnualPhysicalExamViewModel>(ModuleIds.AnnualPhysicalExam);
        Pump(1000);
        Check("Annual Physical Exam uses the shared Civil Status list and has its Edit entries command", ape.CivilStatuses.SequenceEqual(expected) && ape.EditCivilStatusListCommand is not null, $"{ape.CivilStatuses.Count} vs {expected.Count}");
        Snapshot("civil-ape.png");
        var page2 = Open<MedicalExaminationViewModel>(ModuleIds.AnnualPhysicalExamPage2);
        Pump(1000);
        Check("Annual Physical Exam Page 2 uses it too", page2.CivilStatuses.SequenceEqual(expected) && page2.EditCivilStatusListCommand is not null, "");
        var mer = Open<MedicalExamReportViewModel>(ModuleIds.MedicalExamReport);
        Pump(1000);
        Check("so does the Medical Examination Report", mer.CivilStatuses.SequenceEqual(expected) && mer.EditCivilStatusListCommand is not null, "");

        // Annual Physical Exam Page 2: the physician is picked from the Pathologist list and the licence number follows the name.
        var saved = provider.GetRequiredService<IServiceRunner>().RunAsync<DiagnosticLabs.Application.Entries.IEntryService, DiagnosticLabs.Application.Common.Result<DiagnosticLabs.Application.Entries.SingleLineEntryList>>(
            s => s.SaveSingleLineAsync(DiagnosticLabs.Application.Entries.EntryFields.Pathologist, ModuleIds.AnnualPhysicalExamPage2,
                [new DiagnosticLabs.Application.Entries.SingleLineEntry(0, "Dr. Romeo G. Malonzo", "87844")])).GetAwaiter().GetResult();
        Check("a physician with a licence number is saved in the Pathologist list", saved.IsSuccess, saved.IsFailure ? saved.Error.Message : "");
        var page2Again = Open<MedicalExaminationViewModel>(ModuleIds.AnnualPhysicalExamPage2);
        Pump(1500);
        Check("Page 2 offers the physician from the Pathologist list", page2Again.Pathologists.Contains("Dr. Romeo G. Malonzo"), string.Join("|", page2Again.Pathologists));
        Act(() => page2Again.PhysicianName.Value = "Dr. Romeo G. Malonzo", 600);
        Check("picking the physician fills in the licence number", page2Again.PhysicianLicense.Value == "87844", page2Again.PhysicianLicense.Value ?? "(empty)");
        Act(() => page2Again.PhysicianName.Value = "Someone typed in", 600);
        Check("a name that is not in the list gets no licence", string.IsNullOrEmpty(page2Again.PhysicianLicense.Value), page2Again.PhysicianLicense.Value ?? "(empty)");
        Snapshot("civil-page2.png");
    }

    // Printing saves, builds the report and renders two pages; on a busy machine that takes longer than a fixed wait.
    private static void PrintAndWait(MedicalExamReportViewModel vm)
    {
        CapturePreview.Forget();
        Act(() => vm.PrintCommand.Execute(null), 1500);
        for (var i = 0; i < 20 && CapturePreview.Last is null; i++)
            Pump(500);
    }

    private static void MedExamChecks()
    {
        CapturePreview.OutputFolder = _out;
        _main.WindowState = System.Windows.WindowState.Normal;
        _main.Width = 1366;
        _main.Height = 728;
        _main.Left = 0;
        _main.Top = 0;
        Pump(1000);

        var vm = Open<MedicalExamReportViewModel>(ModuleIds.MedicalExamReport);
        Pump(1500);
        Check("the module opens from the menu and starts clean and empty", !vm.IsDirty && vm.Form.Marks.Values.All(g => g.Value is null) && vm.Form.Cells.Values.All(c => c.Value is null), "");
        Check("the OB-Gyne history is open while the sex is not known", vm.IsObGyneEnabled, "");
        SelectTab(0);
        Snapshot("me-1-new-history.png");
        SelectTab(1);
        Snapshot("me-2-new-exam.png");
        SelectTab(2);
        Snapshot("me-3-new-page2.png");
        SelectTab(0);

        Act(() => FillMedExam(vm, longValues: true), 1000);
        Check("typing makes it changed", vm.IsDirty, "");
        Check("the OB-Gyne history stays open for a female applicant", vm.IsObGyneEnabled, "");
        Snapshot("me-4-history.png");
        ScrollAll(380);
        Snapshot("me-5-history-lower.png");
        ScrollAll(null);
        SelectTab(1);
        Snapshot("me-6-exam.png");
        ScrollAll(400);
        Snapshot("me-7-exam-lower.png");
        ScrollAll(null);
        SelectTab(2);
        Snapshot("me-8-page2.png");
        ScrollAll(500);
        Snapshot("me-9-page2-lower.png");
        ScrollAll(null);
        SelectTab(0);

        Act(() => vm.SaveCommand.Execute(null), 2500);
        Check("it saves", vm.Id != 0 && !vm.IsError && !vm.IsDirty, vm.Message);
        var expected = vm.Form.Read();
        Act(() => vm.NewRecordCommand.Execute(null), 800);
        Check("a new form is empty again", vm.Form.Marks.Values.All(g => g.Value is null) && vm.Form.Signatories.All(s => s.Person is null), "");
        Act(() => vm.ToggleSearchCommand.Execute(null));
        Act(() => vm.SearchCommand.Execute(null), 1500);
        Act(() => vm.SelectedItem = vm.Items.First(), 1800);
        var back = vm.Form.Read();
        var wrong = expected.Where(kv => back[kv.Key] != kv.Value).Select(kv => $"{kv.Key}: '{kv.Value}' vs '{back[kv.Key]}'").ToList();
        Check("it reopens with every value in its place", wrong.Count == 0 && !vm.IsDirty && vm.Pathologist == "Dr. Romeo G. Malonzo" && vm.PathologistLicense == "87844" && vm.Remarks!.StartsWith("Applicant"),
            string.Join("; ", wrong.Take(4)) + $" dirty={vm.IsDirty}");
        Act(() => vm.ToggleSearchCommand.Execute(null));

        PrintAndWait(vm);
        Check("print opens the Medical Examination Report with five signatories", CapturePreview.Last is { Layout: DiagnosticLabs.Application.LabResults.ReportLayout.MedicalExamReport } r && r.Signatories.Count == 5,
            (CapturePreview.Last?.Layout.ToString() ?? "none") + $" | message='{vm.Message}' error={vm.IsError} canPrint={vm.CanPrint} dirty={vm.IsDirty}");
        Check("the printout is two pages", CapturePreview.PageCount == 2, CapturePreview.PageCount.ToString());
        File.Copy(Path.Combine(_out, "print-MedicalExamReport.png"), Path.Combine(_out, "print-full-p1.png"), true);
        File.Copy(Path.Combine(_out, "print-MedicalExamReport-p2.png"), Path.Combine(_out, "print-full-p2.png"), true);

        // A short, sparse form: only a few values (empty form must not leave gaps or errors)
        Act(() => vm.NewRecordCommand.Execute(null), 800);
        Act(() =>
        {
            vm.PatientName = "Juan Dela Cruz";
            vm.Age = "30";
            vm.Sex = "Male";
            vm.Form.Marks["HxAsthma"].Value = "Yes";
            vm.Form.Marks["Classification"].Value = "A";
        }, 800);
        Check("the OB-Gyne history is switched off for a male applicant", !vm.IsObGyneEnabled, "");
        PrintAndWait(vm);
        File.Copy(Path.Combine(_out, "print-MedicalExamReport.png"), Path.Combine(_out, "print-sparse-p1.png"), true);
        File.Copy(Path.Combine(_out, "print-MedicalExamReport-p2.png"), Path.Combine(_out, "print-sparse-p2.png"), true);
    }
}
