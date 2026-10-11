using System.Reflection;
using System.Windows;
using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Wpf.ViewModels;

namespace UiHarness;

internal static partial class Program
{
    private static void PatientsDirtyDebug(IServiceProvider provider)
    {
        _main.WindowState = WindowState.Normal;
        Pump(1500);
        var vm = Open<PatientsViewModel>(ModuleIds.Patients);
        Pump(1500);
        var type = vm.GetType();
        object? Get(Type t, string name) => t.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)?.GetValue(vm);
        var baseType = type.BaseType!;
        var baseline = baseType.GetField("_baseline", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(vm);
        var now = baseType.GetMethod("Snapshot", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(vm, null);
        Console.WriteLine("BASELINE " + baseline);
        Console.WriteLine("NOW      " + now);
    }
}
