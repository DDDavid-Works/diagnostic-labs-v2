using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.LabResults;
using DiagnosticLabs.Wpf.Services;
using DiagnosticLabs.Wpf.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace UiHarness;

internal static partial class Program
{
    private static IReadOnlyDictionary<string, string?> SavedDefaults() =>
        Task.Run(() => provider0!.GetRequiredService<IServiceRunner>().RunAsync<IModuleDefaultsService, DiagnosticLabs.Application.Common.Result<IReadOnlyDictionary<string, string?>>>(s => s.GetAsync(ModuleIds.ClinicalChemistry)))
            .GetAwaiter().GetResult().Value;

    private static IServiceProvider? provider0;

    // Does Clinical Chemistry use the same saved-defaults mechanism as the other forms? (read-only unless <paramref name="write"/>)
    private static void CcDefaultsChecks(IServiceProvider provider, bool write)
    {
        provider0 = provider;
        _main.WindowState = System.Windows.WindowState.Normal;
        _main.Width = 1366;
        _main.Height = 728;
        _main.Left = 0;
        _main.Top = 0;
        Pump(1000);

        var saved = SavedDefaults();
        Console.WriteLine($"  saved defaults for module 11: {saved.Count} keys");

        var vm = Open<ClinicalChemistryViewModel>(ModuleIds.ClinicalChemistry);
        Pump(1500);
        var full = vm.Rows.Count(r => r.ConventionalUnit != null && r.ConventionalNormalValue != null && r.SystemUnit != null && r.SystemNormalValue != null);
        Check("the screen reads the saved defaults (HasDefaults)", vm.HasDefaults, "");
        Check("a new form starts with units and ranges on all ten rows", full == 10, $"{full} rows");
        Check("the results are empty", vm.Rows.All(r => r.ConventionalResult is null && r.SystemResult is null), "");
        foreach (var r in vm.Rows)
            Console.WriteLine($"    {r.Label,-22} {r.ConventionalUnit,-6} {r.ConventionalNormalValue,-11} | {r.SystemUnit,-7} {r.SystemNormalValue}");

        // every saved key is one the screen knows (a key the screen does not know would be silently ignored)
        var screenKeys = vm.Rows.SelectMany(r => new[] { r.Name + "ConventionalNormalValue", r.Name + "ConventionalUnit", r.Name + "SystemNormalValue", r.Name + "SystemUnit" }).ToHashSet();
        var unknown = saved.Keys.Where(k => !screenKeys.Contains(k)).ToList();
        Check("every saved key is one the screen reads", unknown.Count == 0, string.Join(", ", unknown));
        Check("every field the screen offers as a default is saved", screenKeys.All(saved.ContainsKey), string.Join(", ", screenKeys.Where(k => !saved.ContainsKey(k))));

        if (!write)
            return;

        // the administrator's own path: Defaults -> change a value -> Save defaults; the same keys are written back
        var before = saved.Keys.OrderBy(k => k).ToList();
        Act(() => vm.EnterDefaultsCommand.Execute(null), 800);
        Check("the Defaults screen opens for an administrator", vm.IsSettingDefaults, "");
        Act(() => vm.Rows[1].ConventionalNormalValue = "50-199", 300);
        Act(() => vm.SaveDefaultsCommand.Execute(null), 2500);
        var after = SavedDefaults();
        Check("saving writes the same set of keys", after.Keys.OrderBy(k => k).SequenceEqual(before), $"{after.Count} keys");
        Check("the changed value is saved", after["CholesterolConventionalNormalValue"] == "50-199", after.GetValueOrDefault("CholesterolConventionalNormalValue") ?? "");
        Check("the other values are kept", after["FastingBloodSugarConventionalNormalValue"] == saved["FastingBloodSugarConventionalNormalValue"], "");
        var again = Open<ClinicalChemistryViewModel>(ModuleIds.ClinicalChemistry);
        Pump(1500);
        Check("the next new form starts with the changed value", again.Rows[1].ConventionalNormalValue == "50-199", again.Rows[1].ConventionalNormalValue ?? "");
        Act(() => again.Rows[0].ConventionalResult = "99", 300);
        Act(() => again.ResetToDefaultsCommand.Execute(null), 500);
        Check("Reset to defaults puts the defaults back", again.Rows[1].ConventionalNormalValue == "50-199" && again.Rows[0].ConventionalUnit == "mg/dL", "");
    }
}
