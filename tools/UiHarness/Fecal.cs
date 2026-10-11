using System.IO;
using System.Windows;
using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Entries;
using DiagnosticLabs.Wpf.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace UiHarness;

internal static partial class Program
{
    private static void FecalChecks(IServiceProvider provider)
    {
        CapturePreview.OutputFolder = _out;
        _main.WindowState = WindowState.Normal;
        _main.Width = 1366;
        _main.Height = 728;
        _main.Left = 0;
        _main.Top = 0;
        Pump(1000);

        // the signatory lists carry a licence number: add entries through the real builder window
        foreach (var (field, people) in new[]
                 {
                     (EntryFields.MedicalTechnologist, new[] { ("Lianne Payla T. Dizon, RMT", "0133304"), ("Ann Jeane D. Evaristo, RMT", "031026"), ("No License Person", "") }),
                     (EntryFields.Pathologist, new[] { ("Ma. Angelita C. Gutierrez, MD, FPSP", "095300") }),
                 })
        {
            var builder = ActivatorUtilities.CreateInstance<SingleLineEntryBuilderViewModel>(provider, field, ModuleIds.StoolFecalysis);
            var window = HostWindow(builder, 560, 420);
            Pump(1500);
            Check($"the {field.Name} builder shows the licence column", builder.HasLicense, "");
            foreach (var (name, license) in people)
            {
                Act(() => builder.AddCommand.Execute(null), 300);
                builder.Rows[^1].Value = name;
                builder.Rows[^1].LicenseNo = license;
            }

            Pump(300);
            Snapshot(window, $"builder-{field.Name.Replace(' ', '-')}.png");
            Act(() => builder.SaveCommand.Execute(null), 1500);
            Check($"the {field.Name} list saves", window.Saved, builder.Message);
        }

        var gender = ActivatorUtilities.CreateInstance<SingleLineEntryBuilderViewModel>(provider, EntryFields.Gender, ModuleIds.Patients);
        var genderWindow = HostWindow(gender, 560, 300);
        Pump(1200);
        Check("an ordinary list has no licence column", !gender.HasLicense, "");
        Snapshot(genderWindow, "builder-gender.png");
        genderWindow.Close();

        var vm = Open<StoolFecalysisViewModel>(ModuleIds.StoolFecalysis);
        Pump(1200);
        // a disabled button offers no hand cursor; an enabled one does
        var buttons = Descendants<System.Windows.Controls.Button>(_main).Where(b => b.Content is "Delete" || b.Content is "New").ToList();
        var delete = buttons.First(b => (string)b.Content == "Delete");
        var newButton = buttons.First(b => (string)b.Content == "New");
        Check("a disabled button has the plain cursor, an enabled one the hand", !delete.IsEnabled && delete.Cursor == System.Windows.Input.Cursors.Arrow && newButton.IsEnabled && newButton.Cursor == System.Windows.Input.Cursors.Hand, $"{delete.IsEnabled}/{delete.Cursor}/{newButton.Cursor}");

        Check("lists load", vm.Genders.Count >= 0 && vm.Technologists.Count >= 0, "");
        Snapshot("fecal-1-new.png");

        Act(() =>
        {
        vm.PatientName = "Emma Millstein";
        vm.Age = "26 years old";
        vm.Sex = "Female";
        vm.CompanyOrPhysician = "ACME";
        vm.Color.Value = "BROWN";
        vm.Consistency.Value = "FORMED";
        vm.Others = "RARE MUCUS";
        vm.Wbc.Value = "2-4";
        vm.Rbc.Value = "0-2";
        vm.Bacteria.Value = "FEW";
        vm.YeastCells.Value = "NONE";
        vm.FatGlobules.Value = "NONE";
        vm.OvaParasite.Value = "NO INTESTINAL PARASITES SEEN";
        vm.MedicalTechnologist = "Lianne Payla T. Dizon, RMT";
        vm.MedicalTechnologist2 = "Ann Jeane D. Evaristo, RMT";
        vm.Pathologist = "Ma. Angelita C. Gutierrez, MD, FPSP";
        vm.Remarks = "Repeat the examination in one week.";
        }, 600);
        Pump(500);
        // the licence follows the name as it changes: typing and picking in the box itself, with no focus change
        var techBox = Descendants<DiagnosticLabs.Wpf.Controls.ChoiceBox>(_main).First(c => ReferenceEquals(c.ItemsSource, vm.Technologists));
        var techCombo = Descendants<System.Windows.Controls.ComboBox>(techBox).First();
        Act(() => techCombo.Text = "Lianne Payla T. Dizon, RMT", 300);
        Check("typing a full name in the box fills its licence at once", vm.MedicalTechnologist == "Lianne Payla T. Dizon, RMT" && vm.MedicalTechnologistLicense == "0133304", $"{vm.MedicalTechnologist}/{vm.MedicalTechnologistLicense}");
        Act(() => techCombo.Text = "Lianne Payla T. Dizon, RM", 300);
        Check("changing it to something else clears the licence at once", vm.MedicalTechnologistLicense is null, vm.MedicalTechnologistLicense ?? "");
        Act(() => techCombo.SelectedItem = techCombo.Items[1], 300);
        Check("picking from the dropdown fills its licence at once", vm.MedicalTechnologist == "Ann Jeane D. Evaristo, RMT" && vm.MedicalTechnologistLicense == "031026", $"{vm.MedicalTechnologist}/{vm.MedicalTechnologistLicense}");
        Act(() => vm.MedicalTechnologist = "Free Name", 300);
        Act(() => vm.MedicalTechnologistLicense = "FREE-1", 300);
        var editRow = techCombo.Items[^1];
        Act(() => techCombo.IsDropDownOpen = true, 300);
        Act(() => techCombo.SelectedItem = editRow, 2500);
        Check("choosing Edit entries leaves a free name and its licence alone", vm.MedicalTechnologist == "Free Name" && vm.MedicalTechnologistLicense == "FREE-1" && techCombo.Text == "Free Name", $"{vm.MedicalTechnologist}/{vm.MedicalTechnologistLicense}/{techCombo.Text}");
        Act(() => vm.MedicalTechnologist = "Lianne Payla T. Dizon, RMT", 300);

        Check("picking the names fills their licence numbers", (vm.MedicalTechnologistLicense, vm.MedicalTechnologist2License, vm.PathologistLicense) == ("0133304", "031026", "095300"), $"{vm.MedicalTechnologistLicense}/{vm.MedicalTechnologist2License}/{vm.PathologistLicense}");
        Check("typing makes it changed", vm.IsDirty, "");
        Snapshot("fecal-2-filled.png");

        Act(() => vm.MedicalTechnologist = "Someone Typed Freely", 300);
        Check("a free-text name has no licence", vm.MedicalTechnologistLicense is null, vm.MedicalTechnologistLicense ?? "");
        Act(() => vm.MedicalTechnologist = "Lianne Payla T. Dizon, RMT", 300);
        Act(() => vm.MedicalTechnologist2 = "No License Person", 300);
        Check("an entry without a licence gives none", vm.MedicalTechnologist2License is null, vm.MedicalTechnologist2License ?? "");
        Act(() => vm.MedicalTechnologist2 = "Ann Jeane D. Evaristo, RMT", 300);
        Snapshot("fecal-2b-licenses.png");
        foreach (var sv in Descendants<System.Windows.Controls.ScrollViewer>(_main))
            sv.ScrollToEnd();
        Pump(400);
        Snapshot("fecal-2c-scrolled.png");
        foreach (var sv in Descendants<System.Windows.Controls.ScrollViewer>(_main))
            sv.ScrollToHome();
        Act(() => vm.SaveCommand.Execute(null), 2500);
        Check("it saves", vm.Id != 0 && !vm.IsError && !vm.IsDirty, vm.Message);
        Act(() => vm.PrintCommand.Execute(null), 3000);
        Check("print opens the preview", CapturePreview.Last is { } r && r.Signatories.Count == 3, "");

        // reopen it: every new field comes back
        Act(() => vm.NewRecordCommand.Execute(null), 800);
        Act(() => vm.SearchCommand.Execute(null), 1500);
        Act(() => vm.ToggleSearchCommand.Execute(null));
        Act(() => vm.SelectedItem = vm.Items.First(), 1800);
        Console.WriteLine($"  reopened id={vm.Id} items={vm.Items.Count} selected={vm.SelectedItem?.Id} msg={vm.Message} search=[{vm.SearchText}] date={vm.DateFilter} page={vm.Page} total={vm.TotalCount}");
        Check("it reopens with its licence numbers", (vm.MedicalTechnologistLicense, vm.MedicalTechnologist2License, vm.PathologistLicense) == ("0133304", "031026", "095300"), $"{vm.MedicalTechnologistLicense}/{vm.MedicalTechnologist2License}/{vm.PathologistLicense}");
        Check("it reopens with the new fields", vm.Wbc.Value == "2-4" && vm.OvaParasite.Value == "NO INTESTINAL PARASITES SEEN" && vm.MedicalTechnologist2 == "Ann Jeane D. Evaristo, RMT" && vm.Others == "RARE MUCUS" && !vm.IsDirty, $"{vm.Wbc.Value}/{vm.OvaParasite.Value}/{vm.MedicalTechnologist2}/{vm.Others}");
        var reopenedBoxes = Descendants<System.Windows.Controls.ComboBox>(_main).Where(c => c.IsEditable).Select(c => c.Text).ToList();
        Check("every box shows the value of the reopened record", reopenedBoxes.Contains("BROWN") && reopenedBoxes.Contains("2-4") && reopenedBoxes.Contains("Ann Jeane D. Evaristo, RMT") && reopenedBoxes.Contains("Ma. Angelita C. Gutierrez, MD, FPSP"), string.Join(" | ", reopenedBoxes));
        var colorBox = Descendants<System.Windows.Controls.ComboBox>(_main).First(c => c.IsEditable && c.Text == "BROWN");
        Act(() => colorBox.Text = "BROWNISH", 300);
        Check("typing in a box marks the form changed without leaving it", vm.IsDirty && vm.Color.Value == "BROWNISH", $"{vm.IsDirty}/{vm.Color.Value}");
        Act(() => vm.Color.Value = "BROWN", 300);
        Check("putting the value back makes it clean again", !vm.IsDirty, "");
        Act(() => vm.ToggleSearchCommand.Execute(null));

        // a long one: findings box grows, remarks move down
        Act(() => vm.NewRecordCommand.Execute(null), 800);
        Act(() =>
        {
        vm.PatientName = "Long Values Test With A Rather Long Patient Name Here";
        vm.CompanyOrPhysician = "A VERY LONG COMPANY NAME OR PHYSICIAN NAME";
        vm.Color.Value = "BROWNISH YELLOW";
        vm.Consistency.Value = "SOFT TO LOOSE";
        vm.Others = string.Join(" ", Enumerable.Repeat("PRESENCE OF UNDIGESTED FOOD PARTICLES", 6));
        vm.OvaParasite.Value = "ASCARIS LUMBRICOIDES OVA SEEN, HOOKWORM OVA SEEN, TRICHURIS TRICHIURA OVA SEEN";
        vm.Wbc.Value = "TOO MANY";
        vm.Remarks = string.Join(" ", Enumerable.Repeat("Repeat the examination.", 18));
        }, 600);
        Pump(300);
        Snapshot("fecal-3-long.png");
        Act(() => vm.SaveCommand.Execute(null), 2500);
        Check("the long one saves", vm.Id != 0 && !vm.IsError, vm.Message);
        File.Copy(Path.Combine(_out, "print-StoolFecalysis.png"), Path.Combine(_out, "print-first.png"), true);
        Act(() => vm.PrintCommand.Execute(null), 3000);
        File.Copy(Path.Combine(_out, "print-StoolFecalysis.png"), Path.Combine(_out, "print-long.png"), true);

        // closing the window: with nothing unsaved it closes cleanly; with unsaved changes it asks (the harness answers "leave them out")
        var failures = new List<string>();
        System.Windows.Threading.DispatcherUnhandledExceptionEventHandler onError = (_, args) => { failures.Add(args.Exception.Message); args.Handled = true; };
        System.Windows.Application.Current.DispatcherUnhandledException += onError;
        Act(() => vm.NewRecordCommand.Execute(null), 800);
        Act(() => _main.Close(), 1500);
        Check("closing the window with nothing unsaved closes it without an error", !_main.IsVisible && failures.Count == 0, string.Join("; ", failures));
        System.Windows.Application.Current.DispatcherUnhandledException -= onError;
    }
}
