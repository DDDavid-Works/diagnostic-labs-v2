using System.IO;
using System.Windows.Media.Imaging;
using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Entries;
using DiagnosticLabs.Application.LabResults;
using DiagnosticLabs.Wpf.Printing;
using DiagnosticLabs.Wpf.Services;
using DiagnosticLabs.Wpf.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace UiHarness;

internal static partial class Program
{
    // The Signatures screen (uploads go through the real picture preparation), the Physician list on the Annual PE and the Medical Examination
    // Report, and every family of signatory block on paper with the signatures in place.
    private static void SignatureChecks(IServiceProvider provider)
    {
        CapturePreview.OutputFolder = _out;
        _main.WindowState = System.Windows.WindowState.Normal;
        _main.Width = 1366;
        _main.Height = 728;
        Pump(500);

        // ---- the lists (names and licences come from the Entry Builder lists) ----
        Task.Run(async () =>
        {
            using var scope = provider.CreateScope();
            var entries = scope.ServiceProvider.GetRequiredService<IEntryService>();
            await entries.SaveSingleLineAsync(EntryFields.MedicalTechnologist, ModuleIds.Signatures,
                [new SingleLineEntry(0, "ANA REYES", "0012345"), new SingleLineEntry(0, "BENJAMIN DELA CRUZ-SANTIAGO", "0067890")]);
            await entries.SaveSingleLineAsync(EntryFields.Pathologist, ModuleIds.Signatures, [new SingleLineEntry(0, "DR. FIORA", "567655")]);
            await entries.SaveSingleLineAsync(EntryFields.Physician, ModuleIds.Signatures, [new SingleLineEntry(0, "DR. MALONZO", "87844")]);
        }).GetAwaiter().GetResult();

        var vm = Open<SignaturesViewModel>(ModuleIds.Signatures);
        Pump(1000);
        Check("the screen shows the three lists", vm.Groups.Select(g => g.People.Count).SequenceEqual([2, 1, 1]), string.Join(", ", vm.Groups.Select(g => $"{g.Title}={g.People.Count}")));
        Snapshot("signatures-1-empty.png");

        SignatoryItem Person(string name) => vm.Groups.SelectMany(g => g.People).Single(p => p.Name == name);
        foreach (var name in new[] { "ANA REYES", "DR. FIORA", "DR. MALONZO", "BENJAMIN DELA CRUZ-SANTIAGO" })
        {
            FakeFiles.SignatureOf = name.Replace("DR. ", "", StringComparison.Ordinal).Split(' ')[0] switch { "BENJAMIN" => "B. dela Cruz", var first => first[..1] + first[1..].ToLowerInvariant() + " R." };
            var person = Person(name);
            Act(() => vm.UploadCommand.Execute(person), 1500);
            Check($"{name}: the signature is saved and shown", person.HasSignature && !vm.IsError, vm.Message);
        }

        FakeFiles.SignatureOf = null;
        FakeFiles.ChooseNotAnImage = true;
        Act(() => vm.RemoveCommand.Execute(Person("BENJAMIN DELA CRUZ-SANTIAGO")), 1200);
        Check("remove takes the signature off", !Person("BENJAMIN DELA CRUZ-SANTIAGO").HasSignature, vm.Message);
        Act(() => vm.UploadCommand.Execute(Person("BENJAMIN DELA CRUZ-SANTIAGO")), 1200);
        Check("a file that is not a picture is refused", vm.IsError && !Person("BENJAMIN DELA CRUZ-SANTIAGO").HasSignature, vm.Message);
        FakeFiles.ChooseNotAnImage = false;
        Snapshot("signatures-2-uploaded.png");

        // ---- what was stored: a trimmed PNG within 600 x 200, paper transparent ----
        var rows = Task.Run(async () =>
        {
            using var scope = provider.CreateScope();
            return (await scope.ServiceProvider.GetRequiredService<ISignatureService>().GetSignatoriesAsync()).Value;
        }).GetAwaiter().GetResult();
        foreach (var row in rows.Where(r => r.Signature is not null))
        {
            var frame = BitmapDecoder.Create(new MemoryStream(row.Signature!), BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
            var pixels = new byte[frame.PixelWidth * frame.PixelHeight * 4];
            new FormatConvertedBitmap(frame, System.Windows.Media.PixelFormats.Bgra32, null, 0).CopyPixels(pixels, frame.PixelWidth * 4, 0);
            var clear = pixels.Where((_, i) => i % 4 == 3).Count(a => a == 0) * 100 / (pixels.Length / 4);
            Check($"{row.Name}: stored PNG {frame.PixelWidth}x{frame.PixelHeight}, {row.Signature!.Length / 1024} KB, {clear}% transparent",
                frame.PixelWidth <= SignatureImages.MaxWidth && frame.PixelHeight <= SignatureImages.MaxHeight && clear > 50 && pixels[3] == 0, "");
            File.WriteAllBytes(Path.Combine(_out, $"stored-{row.ListName}-{row.Name.Replace(".", "")}.png"), row.Signature!);
        }

        // ---- the physician is picked from the Physician list, with its licence ----
        var mer = Open<MedicalExamReportViewModel>(ModuleIds.MedicalExamReport);
        Pump(1500);
        Act(() => mer.Pathologist = "DR. MALONZO", 600);
        Check("Medical Examination Report: the physician list is the Physician list and fills the licence",
            mer.Physicians.Contains("DR. MALONZO") && mer.PathologistLicense == "87844", $"{string.Join("/", mer.Physicians)} lic={mer.PathologistLicense}");
        var ape = Open<AnnualPhysicalExamViewModel>(ModuleIds.AnnualPhysicalExam);
        Pump(1500);
        Act(() => ape.PhysicianName.Value = "DR. MALONZO", 600);
        Check("Annual PE page 2: the physician fills its licence from the Physician list", ape.PhysicianLicense.Value == "87844", ape.PhysicianLicense.Value ?? "(none)");

        // ---- every kind of signatory block on paper ----
        PrintableReport Stamp(PrintableReport report) => Task.Run(async () =>
        {
            using var scope = provider.CreateScope();
            return await SignatureStamp.AttachAsync(scope.ServiceProvider.GetRequiredService<IAppDbContext>(), report);
        }).GetAwaiter().GetResult();

        var letterhead = new ReportLetterhead("BIO ASSAY DIAGNOSTIC CENTER", null, "Angeles City", "888-8888", "bio@yahoo.com", null);
        PrintLine[] patient =
        [
            new("Patient Code", "2026-00000123"), new("Patient Name", "Elena Castillo"), new("Company/Physician", "WALK-IN"),
            new("Age", "35"), new("Sex", "Female"), new("Date Requested", "11/23/2026"),
        ];
        const string footer = "This is a computer generated report. Not valid without signature.";
        PrintSignatory Tech(string name, string role = "Medical Technologist") => new(role, name, name == "ANA REYES" ? "0012345" : "0067890", EntryFields.MedicalTechnologist);
        var pathologist = new PrintSignatory("Pathologist", "DR. FIORA", "567655", EntryFields.Pathologist);
        PrintSignatory[] three = [Tech("ANA REYES"), Tech("BENJAMIN DELA CRUZ-SANTIAGO"), pathologist];
        PrintSignatory[] two = [Tech("ANA REYES"), pathologist];
        PrintableReport Make(ReportLayout layout, string title, IReadOnlyList<PrintSignatory> signers, IReadOnlyDictionary<string, string?>? fields = null, PrintableReport? next = null) =>
            Stamp(new PrintableReport(layout, title, letterhead, patient, [], [new("Remarks", "Normal.")], signers, footer, fields ?? new Dictionary<string, string?>(), NextPage: next));

        var prints = new List<(string Name, PrintableReport Report)>
        {
            ("Hematology (three, whole sheet)", Make(ReportLayout.Hematology, "Hematology", three)),
            ("Fecalysis (three, half sheet)", Make(ReportLayout.StoolFecalysis, "Fecalysis", three)),
            ("Urinalysis (two, half sheet)", Make(ReportLayout.Urinalysis, "Urinalysis", two)),
            ("Serology (two, test result)", Make(ReportLayout.TestResult, "Serology", two)),
            ("Clinical Chemistry 2 (two)", Make(ReportLayout.ClinicalChemistry2, "Clinical Chemistry", two)),
            ("Physical Examination sheet", Make(ReportLayout.PhysicalExamination, "Laboratory Results", two)),
            ("Medical Examination Report", Make(ReportLayout.MedicalExamReport, "Medical Examination Report",
            [
                Tech("ANA REYES", "Vital Signs done by"), Tech("BENJAMIN DELA CRUZ-SANTIAGO", "Interview done by"), Tech("ANA REYES", "Assessment done by"),
                Tech("ANA REYES", "Noted by"), new PrintSignatory("Physician", "DR. MALONZO", "87844", EntryFields.Physician),
            ])),
            ("Annual PE + page 2", Make(ReportLayout.AnnualPhysicalExam, "Annual Physical Examination", [], next: new PrintableReport(
                ReportLayout.MedicalExamination, "Medical Examination Report", letterhead, patient, [], [new("Remarks", "Fit.")],
                [new PrintSignatory("Physician in Charge", "DR. MALONZO", "87844", EntryFields.Physician)], string.Empty,
                new Dictionary<string, string?> { ["PhysicianName"] = "DR. MALONZO", ["PhysicianLicense"] = "87844", ["AssessmentDoneBy"] = "ANA REYES" }))),
        };

        var preview = new CapturePreview();
        foreach (var (name, report) in prints)
        {
            var signed = report.Signatories.Count(s => s.Signature is not null) + (report.NextPage?.Signatories.Count(s => s.Signature is not null) ?? 0);
            Check($"{name}: {signed} signature(s) found by name", report.HasSignatures, "");
            try
            {
                preview.Show(report);
            }
            catch (Exception ex)
            {
                Check($"{name}: laid out", false, ex.Message);
            }
        }

        // The preview's "Print signatures" box cleared: the same page without them.
        var plain = prints[0].Report.WithoutSignatures();
        var document = ReportDocumentBuilder.Build(plain);
        Check("without signatures the page has no picture on it", document.Pages[0].GetPageRoot(false) is { } page
            && ((System.Windows.Controls.Canvas)page.Children[0]).Children.OfType<System.Windows.Controls.Image>().All(i => i.Source is not BitmapImage), "");
    }
}
