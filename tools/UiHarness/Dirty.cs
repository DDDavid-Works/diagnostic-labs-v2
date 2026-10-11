using System.Windows;
using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Wpf.Services;
using DiagnosticLabs.Wpf.ViewModels;

namespace UiHarness;

internal static partial class Program
{
    private static void DirtyChecks(IServiceProvider provider)
    {
        CapturePreview.OutputFolder = _out;
        _main.WindowState = WindowState.Normal;
        _main.Width = 1366;
        _main.Height = 728;
        _main.Left = 0;
        _main.Top = 0;
        Pump(1500);

        // 1. every module opens clean, and so does its first record
        var menu = _mainVm.Menu.SelectMany(g => g.Items).ToList();
        var dirtyOnOpen = new List<string>();
        var checkedModules = 0;
        foreach (var item in menu)
        {
            Act(() => _mainVm.OpenModuleCommand.Execute(item), 1800);
            if (_mainVm.CurrentPage is not IDirtyForm form)
                continue;

            checkedModules++;
            if (form.IsDirty)
            {
                dirtyOnOpen.Add(item.Name + " (new)");
                var bt = _mainVm.CurrentPage.GetType().BaseType!;
                Console.WriteLine("SNAP baseline " + bt.GetField("_baseline", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(_mainVm.CurrentPage));
                Console.WriteLine("SNAP now      " + bt.GetMethod("Snapshot", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(_mainVm.CurrentPage, null));
            }

            dynamic page = _mainVm.CurrentPage;
            try
            {
                var items = ((System.Collections.IEnumerable)page.Items).Cast<object>().ToList();
                if (items.Count > 0)
                {
                    Act(() => page.SelectedItem = items[0], 1800);
                    if (form.IsDirty)
                        dirtyOnOpen.Add(item.Name + " (first record)");
                }
            }
            catch (Exception)
            {
                // a screen without a list
            }
        }

        Check($"{checkedModules} modules open without looking changed", checkedModules > 10 && dirtyOnOpen.Count == 0, string.Join(", ", dirtyOnOpen));

        // 2. an edited form is asked about before another module, Home or Close replaces it
        var stool = Open<StoolFecalysisViewModel>(ModuleIds.StoolFecalysis);
        Check("a new, untouched form is clean", !stool.IsDirty, "");
        stool.PatientName = "Dirty Tester";
        stool.Others = "WIP";
        Check("typing makes it changed", stool.IsDirty, "");

        AutoConfirm.Asked = 0;
        AutoConfirm.NextChoice = SaveChoice.Cancel;
        Act(() => _mainVm.GoHomeCommand.Execute(null), 800);
        Check("Cancel keeps the user on the form", ReferenceEquals(_mainVm.CurrentPage, stool) && AutoConfirm.Asked == 1 && stool.IsDirty, _mainVm.CurrentPage.GetType().Name);
        Act(() => _mainVm.CloseModuleCommand.Execute(null), 800);
        Check("Close module asks too", ReferenceEquals(_mainVm.CurrentPage, stool) && AutoConfirm.Asked == 2, "");

        var other = _mainVm.Menu.SelectMany(g => g.Items).First(i => i.ModuleId == ModuleIds.Urinalysis);
        Act(() => _mainVm.OpenModuleCommand.Execute(other), 800);
        Check("so does opening another module", ReferenceEquals(_mainVm.CurrentPage, stool) && AutoConfirm.Asked == 3, "");

        AutoConfirm.NextChoice = SaveChoice.Discard;
        Act(() => _mainVm.CloseModuleCommand.Execute(null), 1500);
        Check("leaving without saving closes the module", _mainVm.CurrentPage is HomeViewModel && !_mainVm.CanCloseModule, _mainVm.CurrentPage.GetType().Name);

        // 3. Save saves, then leaves
        var stool2 = Open<StoolFecalysisViewModel>(ModuleIds.StoolFecalysis);
        stool2.PatientName = "Dirty Saver";
        stool2.Others = "SAVED ON LEAVING";
        AutoConfirm.NextChoice = SaveChoice.Save;
        Act(() => _mainVm.GoHomeCommand.Execute(null), 3500);
        Check("Save saves the result and then leaves", _mainVm.CurrentPage is HomeViewModel && stool2.Id != 0 && !stool2.IsDirty, $"{_mainVm.CurrentPage.GetType().Name} id {stool2.Id}");

        // 4. a form that cannot be saved stays (the reason is on screen)
        var stool3 = Open<StoolFecalysisViewModel>(ModuleIds.StoolFecalysis);
        stool3.Others = "NO PATIENT";
        Act(() => _mainVm.GoHomeCommand.Execute(null), 2000);
        Check("a refused save keeps the user on the form", ReferenceEquals(_mainVm.CurrentPage, stool3) && stool3.IsError, stool3.Message);

        // 5. opening another record, or New, asks as well
        AutoConfirm.NextChoice = SaveChoice.Cancel;
        Act(() => stool3.NewRecordCommand.Execute(null), 800);
        Check("New asks and Cancel keeps the changes", stool3.Others == "NO PATIENT", stool3.Others ?? "");
        AutoConfirm.NextChoice = SaveChoice.Discard;
        Act(() => stool3.NewRecordCommand.Execute(null), 800);
        Check("New after leaving them out clears the form", string.IsNullOrEmpty(stool3.Others) && !stool3.IsDirty, stool3.Others ?? "");

        Act(() => stool3.SearchText = "Dirty Saver");
        Act(() => stool3.SearchCommand.Execute(null), 1500);
        var saved = stool3.Items.First();
        Act(() => stool3.SelectedItem = saved, 1800);
        Check("a saved record opens clean", stool3.Id == saved.Id && !stool3.IsDirty, "");
        stool3.Others = "CHANGED";
        AutoConfirm.NextChoice = SaveChoice.Cancel;
        var again = stool3.Items.First();
        Act(() => stool3.NewRecordCommand.Execute(null), 800);
        Check("a changed record is asked about before New", stool3.Id == saved.Id && stool3.Others == "CHANGED", "");

        // 6. Company Setup
        AutoConfirm.NextChoice = SaveChoice.Discard;
        Act(() => _mainVm.GoHomeCommand.Execute(null), 1500);
        var setup = _mainVm.Menu.SelectMany(g => g.Items).FirstOrDefault(i => i.ModuleId == ModuleIds.CompanySetup);
        if (setup is not null)
        {
            Act(() => _mainVm.OpenModuleCommand.Execute(setup), 2000);
            var company = (CompanySetupViewModel)_mainVm.CurrentPage;
            Check("Company Setup opens clean", !company.IsDirty, "");
            company.Tagline = "changed tagline";
            AutoConfirm.NextChoice = SaveChoice.Cancel;
            Act(() => _mainVm.GoHomeCommand.Execute(null), 800);
            Check("Company Setup asks when it has changes", ReferenceEquals(_mainVm.CurrentPage, company), "");
            AutoConfirm.NextChoice = SaveChoice.Discard;
            Act(() => _mainVm.GoHomeCommand.Execute(null), 1500);
            Check("and lets go when they are left out", _mainVm.CurrentPage is HomeViewModel, "");
        }
    }
}
