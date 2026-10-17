using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DiagnosticLabs.Application;
using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Auth;
using DiagnosticLabs.Application.Entries;
using DiagnosticLabs.Application.Lookups;
using DiagnosticLabs.Application.Management;
using DiagnosticLabs.Application.Settings;
using DiagnosticLabs.Infrastructure;
using DiagnosticLabs.Wpf.Services;
using DiagnosticLabs.Wpf.ViewModels;
using DiagnosticLabs.Wpf.Views;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace UiHarness;

internal sealed class AutoConfirm : IDialogService
{
    public static DiagnosticLabs.Wpf.Services.SaveChoice NextChoice { get; set; } = DiagnosticLabs.Wpf.Services.SaveChoice.Discard;

    public static int Asked { get; set; }

    public bool Confirm(string message, string title) => true;

    public DiagnosticLabs.Wpf.Services.SaveChoice AskToSave(string message, string title)
    {
        Asked++;
        return NextChoice;
    }
    public void Inform(string message, string title) { }
}

// Stands in for the modal dialog when a screen's "edit list" button is pressed: saves one extra entry, as a user would.
internal sealed class ScriptedBuilder(IServiceProvider provider) : IEntryBuilderDialog
{
    public bool EditSingleLine(EntryField field, int hostModuleId)
    {
        // Run off the UI thread: blocking on async work with a UI synchronization context would deadlock this stand-in.
        return Task.Run(async () =>
        {
            using var scope = provider.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IEntryService>();
            var current = (await service.GetSingleLineAsync(field, hostModuleId)).Value.Items.ToList();
            current.Add(new DiagnosticLabs.Application.Entries.SingleLineEntry(0, "Harness option"));
            return (await service.SaveSingleLineAsync(field, hostModuleId, current)).IsSuccess;
        }).GetAwaiter().GetResult();
    }

    // Stands in for the multi-line Entry Builder window: saves one more reusable text, as a user would.
    public bool EditMultiLine(EntryField field, int hostModuleId)
    {
        return Task.Run(async () =>
        {
            using var scope = provider.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IEntryService>();
            var current = (await service.GetMultiLineAsync(field, hostModuleId)).Value.Items.ToList();
            current.Add(new DiagnosticLabs.Application.Entries.MultiLineEntry(0, $"Normal {field.Name} {current.Count + 1}", "NORMAL\r\nNo abnormal findings."));
            return (await service.SaveMultiLineAsync(field, hostModuleId, current)).IsSuccess;
        }).GetAwaiter().GetResult();
    }
}

// Stands in for the Open File dialog: "chooses" a generated PNG (or a fake one when asked to).
internal sealed class FakeFiles : IFileDialogService
{
    public static bool ChooseNotAnImage { get; set; }

    public PickedFile? PickImage()
    {
        if (ChooseNotAnImage)
            return new PickedFile("notes.png", "this is not an image at all"u8.ToArray());

        var drawing = new System.Windows.Media.DrawingVisual();
        using (var dc = drawing.RenderOpen())
        {
            dc.DrawRoundedRectangle(System.Windows.Media.Brushes.SteelBlue, null, new Rect(0, 0, 160, 160), 24, 24);
            dc.DrawEllipse(System.Windows.Media.Brushes.White, null, new Point(80, 80), 46, 46);
            dc.DrawRectangle(System.Windows.Media.Brushes.Crimson, null, new Rect(72, 44, 16, 72));
            dc.DrawRectangle(System.Windows.Media.Brushes.Crimson, null, new Rect(44, 72, 72, 16));
        }

        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(160, 160, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
        bitmap.Render(drawing);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return new PickedFile("logo.png", stream.ToArray());
    }
}

// Stands in for the modal picker: answers with whatever the check asks for (null = Cancel).
internal sealed class ScriptedPicker : IServicePickerDialog
{
    public static Func<IReadOnlyList<ServiceOption>, IReadOnlyList<long>, IReadOnlyList<long>?> Answer { get; set; } = (_, current) => current;

    public IReadOnlyList<long>? Pick(IReadOnlyList<ServiceOption> options, IEnumerable<long> selectedIds) => Answer(options, [.. selectedIds]);
}

internal sealed class NoPasswordDialog : IPasswordChangeDialog
{
    public bool Show(bool forced) => false;
}

internal static partial class Program
{
    private static MainWindow _main = null!;
    private static MainViewModel _mainVm = null!;
    private static string _out = "";
    private static int _failures;

    [STAThread]
    private static int Main(string[] args)
    {
        if (args[0] == "probe") { MainWindowProbe(); return 0; }
        _out = args[0];
        if (_out.EndsWith("stations")) FakeUpdates.ShareVersion = "1.0.1";
        var connection = args[1];

        var bindingErrors = new List<string>();
        PresentationTraceSources.Refresh();
        PresentationTraceSources.DataBindingSource.Listeners.Add(new CapturingListener(bindingErrors));
        PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Warning;

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:DiagnosticLabs"] = connection })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.TryAddEnumerable(services, Microsoft.Extensions.DependencyInjection.ServiceDescriptor.Singleton<Microsoft.Extensions.Logging.ILoggerProvider, ErrorLogProvider>());
        services.AddApplication().AddInfrastructure(config);
        services.AddSingleton<IServiceRunner, ServiceRunner>();
        services.AddSingleton<IDialogService, AutoConfirm>();
        services.AddSingleton<IEntryBuilderDialog>(sp => new ScriptedBuilder(sp));
        services.AddSingleton<IPasswordChangeDialog>(sp => new NoPasswordDialog());
        services.AddSingleton<IFileDialogService, FakeFiles>();
        services.AddSingleton<IServicePickerDialog, ScriptedPicker>();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IReportPreviewDialog, CapturePreview>();
        services.AddSingleton<DiagnosticLabs.Application.Stations.IAppInfo, FakeAppInfo>();
        services.AddSingleton<IUpdateChecker, FakeUpdates>();
        foreach (var vm in new[]
                 {
                     typeof(PatientsViewModel), typeof(RegistrationsViewModel), typeof(PaymentsViewModel), typeof(StoolFecalysisViewModel), typeof(UrinalysisViewModel), typeof(HematologyViewModel), typeof(PhysicalExaminationViewModel), typeof(ClinicalMicroscopyViewModel), typeof(MedicalExamReportViewModel), typeof(SerologyViewModel), typeof(ImmunologyViewModel), typeof(PregnancyTestViewModel), typeof(ClinicalChemistryViewModel), typeof(ClinicalChemistry1ViewModel), typeof(ClinicalChemistry2ViewModel), typeof(MedicalExaminationViewModel), typeof(HomeViewModel), typeof(AnnualPhysicalExamViewModel), typeof(CompaniesViewModel), typeof(DepartmentsViewModel), typeof(ServicesViewModel),
                     typeof(PackagesViewModel), typeof(ItemsViewModel), typeof(ItemLocationsViewModel), typeof(DiscountsViewModel),
                     typeof(StationsViewModel), typeof(SalesReportViewModel), typeof(BatchPrintViewModel), typeof(StatementOfAccountViewModel), typeof(MainViewModel), typeof(UsersViewModel), typeof(CompanySetupViewModel), typeof(FormDesignerViewModel), typeof(LoginViewModel),
                 })
            services.AddTransient(vm);
        var provider = services.BuildServiceProvider();

        provider.GetRequiredService<ICurrentUserSession>().SignIn(new AuthenticatedUser(1, "Admin", "Test Admin", true, false, []));

        var app = new DiagnosticLabs.Wpf.App();
        app.InitializeComponent();

        _mainVm = provider.GetRequiredService<MainViewModel>();
        _main = new MainWindow(_mainVm)
        {
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = 0,
            Top = 0,
            ShowInTaskbar = false,
            ShowActivated = false,
            WindowState = WindowState.Normal, // the real window opens maximized; the harness cannot (hidden window)
        };
        _main.Show();
        Pump(2500);
        Console.WriteLine($"menu: {string.Join(", ", _mainVm.Menu.Select(g => $"{g.Name}({g.Items.Count})"))}");

        if (_out.EndsWith("registration")) { RegistrationChecks(provider); Console.WriteLine($"binding errors: {bindingErrors.Count}"); foreach (var e in bindingErrors.Take(10)) Console.WriteLine("  " + e); _main.Close(); return _failures; }
        if (_out.EndsWith("payments")) { PaymentChecks(provider); Console.WriteLine($"binding errors: {bindingErrors.Count}"); foreach (var e in bindingErrors.Take(10)) Console.WriteLine("  " + e); _main.Close(); return _failures; }
        if (_out.EndsWith("ccdefaultsread")) { CcDefaultsChecks(provider, false); _main.Close(); return _failures; }
        if (_out.EndsWith("ccdefaultswrite")) { CcDefaultsChecks(provider, true); _main.Close(); return _failures; }
        if (_out.EndsWith("microscopy")) { MicroscopyChecks(); Console.WriteLine($"binding errors: {bindingErrors.Count}"); foreach (var e in bindingErrors.Take(8)) Console.WriteLine("  " + e); _main.Close(); return _failures; }
        if (_out.EndsWith("civil")) { CivilStatusChecks(provider); Console.WriteLine($"binding errors: {bindingErrors.Count}"); foreach (var e in bindingErrors.Take(8)) Console.WriteLine("  " + e); _main.Close(); return _failures; }
        if (_out.EndsWith("medexam")) { MedExamChecks(); Console.WriteLine($"binding errors: {bindingErrors.Count}"); foreach (var e in bindingErrors.Take(8)) Console.WriteLine("  " + e); _main.Close(); return _failures; }
        if (_out.EndsWith("physical")) { PhysicalChecks(); Console.WriteLine($"binding errors: {bindingErrors.Count}"); foreach (var e in bindingErrors.Take(8)) Console.WriteLine("  " + e); _main.Close(); return _failures; }
        if (_out.EndsWith("renames")) { RenameChecks(); Console.WriteLine($"binding errors: {bindingErrors.Count}"); _main.Close(); return _failures; }
        if (_out.EndsWith("fecal")) { FecalChecks(provider); Console.WriteLine($"binding errors: {bindingErrors.Count}"); foreach (var e in bindingErrors.Take(8)) Console.WriteLine("  " + e); _main.Close(); return _failures; }

        if (_out.EndsWith("urinalysis")) { UrinalysisChecks(provider); Console.WriteLine($"binding errors: {bindingErrors.Count}"); foreach (var e in bindingErrors.Take(6)) Console.WriteLine("  " + e); _main.Close(); return _failures; }
        if (_out.EndsWith("ape")) { ApeChecks(provider); Console.WriteLine($"binding errors: {bindingErrors.Count}"); foreach (var e in bindingErrors.Take(8)) Console.WriteLine("  " + e); _main.Close(); return _failures; }
        if (_out.EndsWith("hematology")) { HematologyChecks(provider); Console.WriteLine($"binding errors: {bindingErrors.Count}"); foreach (var e in bindingErrors.Take(8)) Console.WriteLine("  " + e); _main.Close(); return _failures; }
        if (_out.EndsWith("serology")) { SerologyChecks(provider); Console.WriteLine($"binding errors: {bindingErrors.Count}"); foreach (var e in bindingErrors.Take(8)) Console.WriteLine("  " + e); _main.Close(); return _failures; }
        if (_out.EndsWith("pregnancy")) { PregnancyChecks(provider); Console.WriteLine($"binding errors: {bindingErrors.Count}"); foreach (var e in bindingErrors.Take(8)) Console.WriteLine("  " + e); _main.Close(); return _failures; }
        if (_out.EndsWith("pdebug")) { PatientsDirtyDebug(provider); _main.Close(); return _failures; }
        if (_out.EndsWith("dirty")) { DirtyChecks(provider); Console.WriteLine($"binding errors: {bindingErrors.Count}"); _main.Close(); return _failures; }
        if (_out.EndsWith("home")) { HomeChecks(provider); Console.WriteLine($"binding errors: {bindingErrors.Count}"); foreach (var e in bindingErrors.Take(8)) Console.WriteLine("  " + e); _main.Close(); return _failures; }
        if (_out.EndsWith("statement")) { StatementChecks(provider); Console.WriteLine($"binding errors: {bindingErrors.Count}"); foreach (var e in bindingErrors.Take(8)) Console.WriteLine("  " + e); _main.Close(); return _failures; }
        if (_out.EndsWith("batch")) { BatchPrintChecks(provider); Console.WriteLine($"binding errors: {bindingErrors.Count}"); foreach (var e in bindingErrors.Take(8)) Console.WriteLine("  " + e); _main.Close(); return _failures; }
        if (_out.EndsWith("stations")) { StationsChecks(provider); Console.WriteLine($"binding errors: {bindingErrors.Count}"); foreach (var e in bindingErrors.Take(8)) Console.WriteLine("  " + e); if (_main.IsLoaded) _main.Close(); return _failures; }
        if (_out.EndsWith("sales")) { SalesChecks(provider); Console.WriteLine($"binding errors: {bindingErrors.Count}"); foreach (var e in bindingErrors.Take(8)) Console.WriteLine("  " + e); _main.Close(); return _failures; }
        if (_out.EndsWith("mer")) { MedicalExaminationChecks(provider); Console.WriteLine($"binding errors: {bindingErrors.Count}"); foreach (var e in bindingErrors.Take(8)) Console.WriteLine("  " + e); _main.Close(); return _failures; }
        if (_out.EndsWith("chemistry2")) { Chemistry2Checks(provider); Console.WriteLine($"binding errors: {bindingErrors.Count}"); foreach (var e in bindingErrors.Take(8)) Console.WriteLine("  " + e); _main.Close(); return _failures; }
        if (_out.EndsWith("chemistry1")) { Chemistry1Checks(provider); Console.WriteLine($"binding errors: {bindingErrors.Count}"); foreach (var e in bindingErrors.Take(8)) Console.WriteLine("  " + e); _main.Close(); return _failures; }
        if (_out.EndsWith("immunology")) { ImmunologyChecks(provider); Console.WriteLine($"binding errors: {bindingErrors.Count}"); foreach (var e in bindingErrors.Take(8)) Console.WriteLine("  " + e); _main.Close(); return _failures; }
        if (_out.EndsWith("picker")) { ServicePickerChecks(provider); _main.Close(); return _failures; }
        if (_out.EndsWith("company")) { CompanyChecks(provider); _main.Close(); return _failures; }
        if (_out.EndsWith("designer")) { DesignerChecks(provider); Console.WriteLine($"binding errors: {bindingErrors.Count}"); foreach (var e in bindingErrors.Take(8)) Console.WriteLine("  " + e); _main.Close(); return _failures; }
        if (_out.EndsWith("users")) { UsersChecks(provider); _main.Close(); return _failures; }
        if (_out.EndsWith("builder")) { BuilderChecks(provider); _main.Close(); return _failures; }
        if (_out.EndsWith("small")) { SmallScreenShots(); _main.Close(); return 0; }
        if (_out.EndsWith("layout")) { LayoutShots(); _main.Close(); return 0; }

        Departments();
        ItemLocations();
        Services();
        Companies();
        Items();
        Discounts();
        Packages();
        PatientsSmoke();

        Console.WriteLine($"binding errors: {bindingErrors.Count}");
        foreach (var error in bindingErrors.Take(10))
            Console.WriteLine("  " + error);
        Console.WriteLine(_failures == 0 && bindingErrors.Count == 0 ? "ALL CHECKS PASSED" : $"CHECK FAILURES: {_failures}");
        _main.Close();
        return _failures + bindingErrors.Count;
    }

    private static void CompanyChecks(IServiceProvider provider)
    {
        _main.WindowState = WindowState.Normal;
        _main.Width = 1366;
        _main.Height = 728;
        _main.Left = 0;
        _main.Top = 0;
        Pump(800);

        var vm = Open<CompanySetupViewModel>(ModuleIds.CompanySetup);
        Check("the migrated setup is loaded", vm.CompanyName.Length > 0 && vm.Code == "BADC", $"'{vm.CompanyName}' / {vm.Code}");
        Check("no logo yet", !vm.HasLogo, "");
        Snapshot("company-loaded.png");

        vm.CompanyName = "Bayside Diagnostic Center";
        vm.SubCompanyName = "Main Branch";
        vm.Tagline = "Results you can trust";
        vm.Address = "1 Harbor Road, Bayside";
        vm.ContactNumbers = "0917 000 0000";
        vm.Email = "lab@bayside.example";
        vm.Code = "badc";
        Act(() => vm.ChooseLogoCommand.Execute(null));
        Check("choosing a logo shows a preview", vm.HasLogo && vm.Message.Contains("logo.png"), vm.Message);

        FakeFiles.ChooseNotAnImage = true;
        var before = vm.LogoImage;
        Act(() => vm.ChooseLogoCommand.Execute(null));
        Check("a file that is not an image is refused and the logo is kept", vm.IsError && ReferenceEquals(vm.LogoImage, before), vm.Message);
        FakeFiles.ChooseNotAnImage = false;

        vm.Code = "BA-DC";
        Act(() => vm.SaveCommand.Execute(null), 2500);
        Check("a code with a hyphen is refused", vm.IsError && vm.Message.Contains("letters and numbers"), vm.Message);

        vm.Code = "badc";
        Act(() => vm.SaveCommand.Execute(null), 2500);
        Check("saved", !vm.IsError && vm.Message.StartsWith("Saved") && vm.Code == "BADC", vm.Message);
        Snapshot("company-saved.png");

        vm.CompanyName = "Typed but not saved";
        Act(() => vm.RevertCommand.Execute(null), 2000);
        Check("discard reloads what was saved, logo included", vm.CompanyName == "Bayside Diagnostic Center" && vm.HasLogo, vm.CompanyName);

        // the sign-in window shows the branding before anyone is signed in
        var session = provider.GetRequiredService<ICurrentUserSession>();
        session.SignOut();
        var login = provider.GetRequiredService<LoginViewModel>();
        var loginWindow = new LoginWindow(login) { WindowStartupLocation = WindowStartupLocation.Manual, Left = 0, Top = 0, ShowInTaskbar = false, ShowActivated = false };
        loginWindow.Show();
        Pump(2500);
        Check("sign-in window shows the company name, tagline and logo", login.BrandName == "Bayside Diagnostic Center" && login.BrandTagline == "Results you can trust" && login.HasLogo, $"{login.BrandName} / {login.BrandTagline} / logo={login.HasLogo}");
        Snapshot(loginWindow, "company-login.png");
        loginWindow.Close();
        session.SignIn(new AuthenticatedUser(1, "Admin", "Test Admin", true, false, []));

        Act(() => vm.RemoveLogoCommand.Execute(null));
        Act(() => vm.SaveCommand.Execute(null), 2500);
        Check("logo removed and saved", !vm.IsError && !vm.HasLogo, vm.Message);
    }

    private static void UsersChecks(IServiceProvider provider)
    {
        // The company's monitors are 1366x768.
        _main.WindowState = WindowState.Normal;
        _main.Width = 1366;
        _main.Height = 728;
        _main.Left = 0;
        _main.Top = 0;
        Pump(800);

        var vm = Open<UsersViewModel>(ModuleIds.Users);
        Check("users list loaded", vm.Items.Count >= 1, $"{vm.TotalCount} users");
        var groups = vm.PermissionGroups.Select(g => g.Name).ToList();
        Check("grid lists only non-admin groups", groups.SequenceEqual(["Registration", "Sales", "Lab Results", "Reports"]), string.Join(", ", groups));
        var rows = vm.PermissionGroups.SelectMany(g => g.Rows).ToList();
        Check("Change Password is not a permission", rows.All(r => r.ModuleId != 28), $"{rows.Count} rows");
        var patients = rows.First(r => r.ModuleName == "Patients");
        var sales = rows.First(r => r.ModuleName == "Sales Report");
        Check("an unsupported action is greyed out", !sales.SupportsCreate && sales.SupportsPrint, "");

        Act(() => vm.NewRecordCommand.Execute(null));
        Check("new user gets a valid temporary password", PasswordPolicy.Validate(vm.TemporaryPassword).Count == 0, vm.TemporaryPassword);
        var temp = vm.TemporaryPassword;
        vm.Username = "harness.clerk";
        vm.FullName = "Harness Clerk";
        patients.AllowCreate = true;
        Check("ticking an action grants access", patients.HasAccess, "");
        sales.HasAccess = true;
        Check("access alone is view only", sales.HasAccess && !sales.AllowPrint, "");
        Act(() => vm.SaveCommand.Execute(null), 3000);
        Check("user created with guidance", vm.Id != 0 && !vm.IsError && vm.Message.StartsWith("User created"), vm.Message);

        var created = vm.Id;
        Act(() => vm.NewRecordCommand.Execute(null));
        Act(() => vm.SelectedItem = vm.Items.FirstOrDefault(i => i.Username == "harness.clerk"));
        Pump(1200);
        Snapshot("users-permissions-top.png");
        ScrollForm(_main, 330);
        Snapshot("users-permissions.png");
        ScrollForm(_main, 0);
        Check("permissions come back after reopening", vm.Id == created && patients.AllowCreate && sales.HasAccess && !sales.AllowPrint, $"{vm.Username}");

        patients.HasAccess = false;
        Check("removing access clears the actions", !patients.AllowCreate, "");

        vm.IsAdmin = true;
        Check("administrators do not need the grid", !vm.ShowPermissions, "");
        vm.IsAdmin = false;
        Check("the grid is back for a regular user", vm.ShowPermissions, "");

        Act(() => vm.SaveCommand.Execute(null), 3000);
        Check("saving another user explains when it takes effect", !vm.IsError && vm.Message.Contains("close and reopen"), vm.Message);

        Check("copy-from lists active users", vm.UserOptions.Count >= 2, string.Join(" | ", vm.UserOptions.Select(o => o.Name)));
        Act(() => vm.CopyFromUser = vm.UserOptions.First(o => o.Name.Contains("Harness Clerk")));
        patients.HasAccess = true;
        patients.AllowEdit = true;
        Act(() => vm.CopyPermissionsCommand.Execute(null));
        Check("copying restores the other user's rows", !patients.HasAccess && sales.HasAccess, vm.Message);

        vm.TemporaryPassword = "Reset-pass-9";
        Act(() => vm.ResetPasswordCommand.Execute(null), 3000);
        Check("password reset", !vm.IsError && vm.Message.StartsWith("Password reset"), vm.Message);
        Check("status says the user must change it", vm.AccountStatus.Contains("new password"), vm.AccountStatus);
        Act(() => vm.UnlockCommand.Execute(null), 2000);
        Check("unlock works on an unlocked account", !vm.IsError, vm.Message);

        // the new user, signed in with the temporary password, can change it
        var session = provider.GetRequiredService<ICurrentUserSession>();
        session.SignIn(new AuthenticatedUser(created, "harness.clerk", "Harness Clerk", false, true, []));
        var cp = ActivatorUtilities.CreateInstance<ChangePasswordViewModel>(provider, true);
        var cw = new ChangePasswordWindow(cp) { WindowStartupLocation = WindowStartupLocation.Manual, Left = 0, Top = 0, ShowInTaskbar = false, ShowActivated = false };
        cw.Show();
        Pump(1200);
        Snapshot(cw, "change-password-forced.png");
        cp.OldPassword = "wrong-password";
        cp.NewPassword = "Brand-new-pass-1";
        cp.ConfirmPassword = "Brand-new-pass-1";
        Act(() => cp.SaveCommand.Execute(null), 2500);
        Check("wrong current password keeps the dialog open", cp.IsError && cw.IsVisible, cp.Message);
        cp.OldPassword = "Reset-pass-9";
        cp.NewPassword = "short";
        cp.ConfirmPassword = "short";
        Act(() => cp.SaveCommand.Execute(null), 2500);
        Check("a weak new password is refused", cp.IsError && cw.IsVisible, cp.Message);
        cp.NewPassword = "Brand-new-pass-1";
        cp.ConfirmPassword = "Brand-new-pass-1";
        Act(() => cp.SaveCommand.Execute(null), 3000);
        Check("changing the password closes the dialog and clears the flag", cw.Changed && !cw.IsVisible && !session.MustChangePassword, cp.Message);

        // back to the administrator for the safeguard checks
        session.SignIn(new AuthenticatedUser(1, "Admin", "Test Admin", true, false, []));
        Act(() => vm.SearchText = "Admin");
        Act(() => vm.SearchCommand.Execute(null));
        Act(() => vm.SelectedItem = vm.Items.FirstOrDefault(i => i.Username == "Admin"));
        Pump(1200);
        Act(() => vm.DeleteCommand.Execute(null), 2000);
        Check("you can not deactivate yourself", vm.IsError && vm.Message.Contains("own account"), vm.Message);
        Snapshot("users-form.png");
        Act(() => vm.ToggleSearchCommand.Execute(null));
        Snapshot("users-list.png");

        // clean up the account created by this check
        Act(() => vm.SearchText = "harness.clerk");
        Act(() => vm.SearchCommand.Execute(null));
        Act(() => vm.SelectedItem = vm.Items.FirstOrDefault());
        Pump(1200);
        Act(() => vm.DeleteCommand.Execute(null), 2000);
    }

    private static void ScrollForm(DependencyObject root, double offset)
    {
        ScrollRecursive(root, offset);
        Pump(300);
    }

    private static void ScrollRecursive(DependencyObject root, double offset)
    {
        for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            if (child is System.Windows.Controls.ScrollViewer { ScrollableHeight: > 0 } sv)
                sv.ScrollToVerticalOffset(offset);
            ScrollRecursive(child, offset);
        }
    }

    private static void BuilderChecks(IServiceProvider provider)
    {
        // --- the single-line builder on the general Gender list
        var vm = ActivatorUtilities.CreateInstance<SingleLineEntryBuilderViewModel>(provider, EntryFields.Gender, ModuleIds.Patients);
        var win = HostWindow(vm, 420, 520);
        Pump(2000);
        Check("builder lists the current entries in order", vm.Rows.Select(r => r.Value).SequenceEqual(["Male", "Female"]), string.Join(", ", vm.Rows.Select(r => r.Value)));
        Check("builder header names the field and scope", vm.FieldName == "Gender" && vm.ScopeName.StartsWith("General list"), $"{vm.FieldName} / {vm.ScopeName}");
        Snapshot(win, "builder-single.png");

        Act(() => vm.AddCommand.Execute(null));
        vm.Rows[^1].Value = "male"; // duplicate of an existing entry, different case
        Act(() => vm.SaveCommand.Execute(null));
        Check("duplicate is refused and the dialog stays open", vm.IsError && win.IsVisible && !win.Saved, vm.Message);

        vm.Rows[^1].Value = "Prefer not to say";
        Act(() => vm.SaveCommand.Execute(null));
        Check("save closes the dialog as saved", win.Saved && !win.IsVisible, vm.Message);

        var vm2 = ActivatorUtilities.CreateInstance<SingleLineEntryBuilderViewModel>(provider, EntryFields.Gender, ModuleIds.Patients);
        var win2 = HostWindow(vm2, 420, 520);
        Pump(1500);
        Check("reopened list has the new entry last", vm2.Rows.Select(r => r.Value).SequenceEqual(["Male", "Female", "Prefer not to say"]), string.Join(", ", vm2.Rows.Select(r => r.Value)));
        Act(() => vm2.CancelCommand.Execute(null));
        Check("cancel closes without saving", !win2.Saved && !win2.IsVisible, "");

        // --- a blank new row is dropped instead of rejected
        var vm3 = ActivatorUtilities.CreateInstance<SingleLineEntryBuilderViewModel>(provider, EntryFields.CivilStatus, ModuleIds.Patients);
        var win3 = HostWindow(vm3, 420, 520);
        Pump(1500);
        Act(() => vm3.AddCommand.Execute(null));
        Act(() => vm3.SaveCommand.Execute(null));
        Check("an untouched new row does not block saving", win3.Saved, vm3.Message);

        // --- the multi-line builder on a module-specific list (Hematology / Result)
        var field = new EntryField("Result", EntryKind.MultiLine, 7);
        var mv = ActivatorUtilities.CreateInstance<MultiLineEntryBuilderViewModel>(provider, field, 7);
        var mw = HostWindow(mv, 760, 520);
        Pump(1500);
        Check("multi-line header shows the module name", mv.FieldName == "Result" && mv.ScopeName == "Hematology", $"{mv.FieldName} / {mv.ScopeName}");
        Act(() => mv.AddCommand.Execute(null));
        mv.SelectedRow!.Title = "Normal";
        mv.SelectedRow.Text = "All values are within range.\r\nNo further action.";
        Act(() => mv.AddCommand.Execute(null));
        mv.SelectedRow!.Title = "Anemia";
        mv.SelectedRow.Text = "Low hemoglobin.";
        Check("selected row follows the editor", mv.Rows.Count == 2 && mv.SelectedRow.Title == "Anemia", "");
        Snapshot(mw, "builder-multi.png");
        Act(() => mv.SaveCommand.Execute(null));
        Check("multi-line save closes the dialog", mw.Saved && !mw.IsVisible, mv.Message);

        var mv2 = ActivatorUtilities.CreateInstance<MultiLineEntryBuilderViewModel>(provider, field, 7);
        HostWindow(mv2, 760, 520);
        Pump(1500);
        Check("multi-line templates are saved in order with their text", mv2.Rows.Select(r => r.Title).SequenceEqual(["Normal", "Anemia"]) && mv2.Rows[0].Text.Contains("No further action"), string.Join(", ", mv2.Rows.Select(r => r.Title)));
        Act(() => mv2.CancelCommand.Execute(null));

        // --- the Patients screen: edit-list button reloads the dropdown; a typed value is not added to the list
        var patients = Open<PatientsViewModel>(ModuleIds.Patients);
        Check("gender dropdown has the saved entries", patients.Genders.Contains("Prefer not to say"), string.Join(", ", patients.Genders));
        Act(() => patients.EditGenderListCommand.Execute(null));
        Check("pressing the edit button refreshes the dropdown", patients.Genders.Contains("Harness option"), string.Join(", ", patients.Genders));
        Act(() => patients.NewRecordCommand.Execute(null));
        patients.PatientName = "Typed Value Patient";
        patients.Sex = "Typed only";
        Act(() => patients.SaveCommand.Execute(null), 2500);
        Check("typed gender is kept on the patient", !patients.IsError && patients.Sex == "Typed only", patients.Message);
        Check("typed gender is not added to the list", !patients.Genders.Contains("Typed only"), string.Join(", ", patients.Genders));
        Snapshot("patients-choicebox.png");
        Act(() => patients.DeleteCommand.Execute(null), 2000);
    }

    private static EntryBuilderWindow HostWindow(EntryBuilderViewModel vm, double width, double height)
    {
        var window = new EntryBuilderWindow(vm)
        {
            Width = width,
            Height = height,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = 0,
            Top = 0,
            ShowInTaskbar = false,
            ShowActivated = false,
        };
        window.Show();
        return window;
    }

    private static void Snapshot(Window window, string file)
    {
        window.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(window);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(_out, file));
        encoder.Save(stream);
    }

    // Does Application.MainWindow still point at the window inside its own Closed event?
    private static void MainWindowProbe()
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var w = new Window { ShowActivated = false, ShowInTaskbar = false, Width = 200, Height = 100 };
        app.MainWindow = w;
        w.Closing += (_, _) => Console.WriteLine($"Closing: MainWindow == w ? {app.MainWindow == w}");
        w.Closed += (_, _) => Console.WriteLine($"Closed:  MainWindow == w ? {app.MainWindow == w}  (null: {app.MainWindow is null})");
        w.Show();
        w.Close();
        Console.WriteLine($"after:   MainWindow == w ? {app.MainWindow == w}");
    }

    // The company's monitors are 1366x768: ~1366x728 usable once the taskbar is counted.
    private static void SmallScreenShots()
    {
        _main.WindowState = WindowState.Normal;
        _main.Width = double.Parse(Environment.GetEnvironmentVariable("HW") ?? "1366");
        _main.Height = 728;
        _main.Left = 0;
        _main.Top = 0;
        Pump(800);

        foreach (var (id, name) in new[] { (ModuleIds.Patients, "patients"), (ModuleIds.Packages, "packages"), (ModuleIds.Items, "items"), (ModuleIds.Discounts, "discounts"), (ModuleIds.Companies, "companies") })
        {
            Open<object>(id);
            var page = _mainVm.CurrentPage;
            Snapshot($"small-{name}-hidden.png");
            dynamic vm = page;
            Act(() => vm.ToggleSearchCommand.Execute(null));
            Snapshot($"small-{name}-list.png");
        }
    }

    private static void LayoutShots()
    {
        var vm = Open<PatientsViewModel>(ModuleIds.Patients);
        Snapshot("layout-1-hidden.png");
        Act(() => vm.ToggleSearchCommand.Execute(null));
        Snapshot("layout-2-list-open.png");
        Act(() => vm.SelectedItem = vm.Items.First(i => i.PatientName.Contains("ARSEID")));
        Snapshot("layout-3-record-selected.png");
        Console.WriteLine($"search visible={vm.IsSearchVisible} items={vm.Items.Count} selected={vm.PatientName}");
        Act(() => vm.ToggleSearchCommand.Execute(null));
        Console.WriteLine($"after second toggle visible={vm.IsSearchVisible}");
    }

    // ------------------------------------------------------------------ flows

    private static void Departments()
    {
        var vm = Open<DepartmentsViewModel>(ModuleIds.Departments);
        Check("departments list loaded", vm.Items.Count > 0, $"{vm.TotalCount} rows");
        Act(() => vm.NewRecordCommand.Execute(null));
        vm.DepartmentName = "Harness Dept";
        Act(() => vm.SaveCommand.Execute(null));
        Check("department created", vm.Id != 0 && !vm.IsError, vm.Message);
        vm.DepartmentDescription = "changed";
        Act(() => vm.SaveCommand.Execute(null));
        Check("department edited", !vm.IsError, vm.Message);
        Act(() => vm.DeleteCommand.Execute(null));
        Check("department deleted (deactivated)", !vm.IsError && vm.Id == 0, vm.Message);
    }

    private static void ItemLocations()
    {
        var vm = Open<ItemLocationsViewModel>(ModuleIds.ItemLocations);
        Act(() => vm.NewRecordCommand.Execute(null));
        vm.ItemLocationName = "Harness Shelf";
        Act(() => vm.SaveCommand.Execute(null));
        Check("item location created", vm.Id != 0 && !vm.IsError, vm.Message);
        Act(() => vm.NewRecordCommand.Execute(null));
        vm.ItemLocationName = "Harness Cabinet";
        Act(() => vm.SaveCommand.Execute(null));
        Check("second item location created", vm.Id != 0 && !vm.IsError, vm.Message);
    }

    private static void Services()
    {
        var vm = Open<ServicesViewModel>(ModuleIds.Services);
        Check("services list loaded", vm.TotalCount >= 13, $"{vm.TotalCount} rows");

        Act(() => vm.SaveCommand.Execute(null));
        Check("empty service rejected", vm.IsError && vm.Message.Contains("Name"), vm.Message);

        foreach (var (name, price) in new[] { ("Harness CBC", 250m), ("Harness X-ray", 400m) })
        {
            Act(() => vm.NewRecordCommand.Execute(null));
            vm.ServiceName = name;
            vm.ServiceDescription = name + " description";
            vm.Price = price;
            Act(() => vm.SaveCommand.Execute(null));
            Check($"service '{name}' created", vm.Id != 0 && !vm.IsError, vm.Message);
        }

        vm.Price = 275m;
        Act(() => vm.SaveCommand.Execute(null));
        Check("service price edited", !vm.IsError && vm.Price == 275m, vm.Message);

        vm.SearchText = "Harness";
        Act(() => vm.SearchCommand.Execute(null));
        Check("service search finds the two new rows", vm.Items.Count == 2, $"{vm.Items.Count} rows");
    }

    private static void Companies()
    {
        var vm = Open<CompaniesViewModel>(ModuleIds.Companies);
        Act(() => vm.NewRecordCommand.Execute(null));
        vm.CompanyName = "Harness Corp";
        vm.ContactPerson = "Ann";
        Act(() => vm.SaveCommand.Execute(null));
        Check("company created", vm.Id != 0 && !vm.IsError && vm.CanSave, vm.Message);
        Snapshot("companies.png");
    }

    private static void Items()
    {
        var vm = Open<ItemsViewModel>(ModuleIds.Items);
        Check("locations available for stock rows", vm.Locations.Count >= 2, $"{vm.Locations.Count} locations");
        Act(() => vm.NewRecordCommand.Execute(null));
        vm.ItemName = "Harness Syringe";
        vm.Cost = 5.5m;
        Act(() => vm.AddQuantityCommand.Execute(null));
        Act(() => vm.AddQuantityCommand.Execute(null));
        Check("two distinct locations offered", vm.Quantities.Count == 2 && vm.Quantities[0].ItemLocationId != vm.Quantities[1].ItemLocationId, "");
        vm.Quantities[0].Quantity = 10;
        vm.Quantities[1].Quantity = 2.5m;
        Check("total updates live", vm.TotalQuantity == 12.5m, vm.TotalQuantity.ToString());
        Act(() => vm.SaveCommand.Execute(null));
        Check("item with stock saved", vm.Id != 0 && !vm.IsError && vm.Quantities.Count == 2, vm.Message);
        Snapshot("items.png");

        Act(() => vm.SelectedQuantity = vm.Quantities.First(q => q.Quantity == 2.5m)); // the list is re-sorted after saving
        Act(() => vm.RemoveQuantityCommand.Execute(null));
        Act(() => vm.SaveCommand.Execute(null));
        Check("stock row removed", !vm.IsError && vm.Quantities.Count == 1 && vm.TotalQuantity == 10m, $"{vm.Quantities.Count} rows, total {vm.TotalQuantity}");
    }

    private static void Discounts()
    {
        var vm = Open<DiscountsViewModel>(ModuleIds.Discounts);
        Act(() => vm.NewRecordCommand.Execute(null));
        vm.DiscountName = "Harness Senior";
        vm.DiscountDescription = "Senior citizen";
        Act(() => vm.AddLineCommand.Execute(null));
        vm.Lines[0].Value = 20;
        Act(() => vm.AddLineCommand.Execute(null));
        vm.Lines[1].IsPercentage = false;
        vm.Lines[1].Value = 50;
        Act(() => vm.SaveCommand.Execute(null));
        Check("discount with two options saved", vm.Id != 0 && !vm.IsError && vm.Lines.Count == 2, vm.Message);

        vm.Lines[1].Value = null; // an option without a value
        Act(() => vm.SaveCommand.Execute(null));
        Check("an option without a value is rejected", vm.IsError, vm.Message);
    }

    private static void Packages()
    {
        var vm = Open<PackagesViewModel>(ModuleIds.Packages);
        var options = vm.ServiceOptions.ToList();
        Check("services and companies available", options.Count >= 3 && vm.Companies.Count >= 2, $"{options.Count} services, {vm.Companies.Count} companies");
        Check("services are offered in creation order", options.Select(o => o.Id).SequenceEqual(options.Select(o => o.Id).OrderBy(i => i)), string.Join(", ", options.Take(3).Select(o => o.Name)));
        var (a, b, c) = (options[0], options[1], options[2]);

        Act(() => vm.NewRecordCommand.Execute(null));
        vm.PackageName = "Harness Pre-employment";
        vm.PackageDescription = "Basic";

        ScriptedPicker.Answer = (_, _) => [a.Id, b.Id];
        Act(() => vm.ChooseServicesCommand.Execute(null));
        Check("the picked services are added in list order at their standard prices",
            vm.Services.Select(s => s.ServiceId).SequenceEqual([a.Id, b.Id]) && vm.Services[0].Price == a.Price && vm.Services[0].ServiceName == a.Name,
            string.Join(", ", vm.Services.Select(s => $"{s.ServiceName}={s.Price}")));
        Check("the package price follows the services", vm.Price == a.Price + b.Price, vm.Price.ToString());

        vm.Services[1].Price = 55m; // a special price for the second service
        Check("editing a line price updates the total", vm.Price == a.Price + 55m, vm.Price.ToString());

        ScriptedPicker.Answer = (_, current) => current;
        Act(() => vm.ChooseServicesCommand.Execute(null));
        Check("pressing Ok without changes keeps everything, special price included", vm.Services.Count == 2 && vm.Services[1].Price == 55m, "");

        ScriptedPicker.Answer = (_, _) => null;
        Act(() => vm.ChooseServicesCommand.Execute(null));
        Check("Cancel changes nothing", vm.Services.Count == 2, "");

        ScriptedPicker.Answer = (_, _) => [b.Id, c.Id]; // untick the first, keep the second, tick a new third
        Act(() => vm.ChooseServicesCommand.Execute(null));
        Check("unticked services are removed, kept ones keep their price, new ones come in",
            vm.Services.Select(s => s.ServiceId).SequenceEqual([b.Id, c.Id]) && vm.Services[0].Price == 55m && vm.Services[1].Price == c.Price,
            string.Join(", ", vm.Services.Select(s => $"{s.ServiceName}={s.Price}")));

        var company = vm.Companies.First(x => x.Name == "Harness Corp");
        vm.CompanyChoiceId = company.Id;
        Act(() => vm.SaveCommand.Execute(null));
        Check("package saved with its services and company", vm.Id != 0 && !vm.IsError && vm.Services.Count == 2, vm.Message);
        Check("the table keeps the service names after saving", vm.Services.All(s => s.ServiceName.Length > 0 && !s.ServiceName.StartsWith("Service ")), string.Join(", ", vm.Services.Select(s => s.ServiceName)));
        Snapshot("packages-table.png");

        vm.Price = 999m;
        Act(() => vm.RemoveServiceCommand.Execute(vm.Services[0]));
        Check("the remove button takes the row out; a typed package price stays", vm.Services.Count == 1 && vm.Price == 999m, vm.Price.ToString());
        Act(() => vm.SaveCommand.Execute(null));
        Check("package edited", !vm.IsError && vm.Services.Count == 1 && vm.Price == 999m, vm.Message);

        // a service switched off after it was added must not vanish when Ok is pressed
        var keep = vm.Services[0].ServiceId;
        Act(() => vm.NewRecordCommand.Execute(null));
        Act(() => vm.SelectedItem = vm.Items.FirstOrDefault(i => i.PackageName == "Harness Pre-employment"));
        Pump(1200);
        Check("reopened package still names its service", vm.Services.Single().ServiceId == keep, vm.Services.Single().ServiceName);
    }

    private static void ServicePickerChecks(IServiceProvider provider)
    {
        _main.WindowState = WindowState.Normal;
        _main.Width = 1366;
        _main.Height = 728;
        Pump(500);

        IReadOnlyList<ServiceOption> options;
        using (var scope = provider.CreateScope())
            options = scope.ServiceProvider.GetRequiredService<IReferenceLookups>().GetServicesAsync().GetAwaiter().GetResult();

        var current = new[] { options[0].Id, options[2].Id };
        var vm = new ServicePickerViewModel(options, current);
        Check("every service is a tile, in order", vm.Tiles.Select(t => t.Id).SequenceEqual(options.Select(o => o.Id)), $"{vm.Tiles.Count} tiles");
        Check("the ones already on the list are ticked", vm.SelectedIds.SequenceEqual(current), vm.Summary);
        Check("the summary counts and totals", vm.SelectedCount == 2 && vm.SelectedTotal == options[0].Price + options[2].Price, vm.Summary);

        vm.SearchText = "chem";
        Check("search narrows the tiles", vm.VisibleTiles.Count > 0 && vm.VisibleTiles.All(t => t.Name.Contains("chem", StringComparison.OrdinalIgnoreCase)) && vm.VisibleTiles.Count < vm.Tiles.Count, $"{vm.VisibleTiles.Count} of {vm.Tiles.Count}");
        vm.VisibleTiles[0].IsSelected = true;
        vm.SearchText = "zzzz-no-match";
        Check("an empty search result says so", vm.NothingMatches, "");
        vm.SearchText = string.Empty;
        Check("selections survive searching, and the summary counts hidden ones", vm.SelectedCount == 3 && vm.VisibleTiles.Count == vm.Tiles.Count, vm.Summary);

        var window = new ServicePickerWindow(vm) { WindowStartupLocation = WindowStartupLocation.Manual, Left = 0, Top = 0, ShowInTaskbar = false, ShowActivated = false };
        window.Show();
        Pump(1500);
        Snapshot(window, "picker-all.png");
        vm.SearchText = "chem";
        Pump(500);
        Snapshot(window, "picker-search.png");
        vm.SearchText = string.Empty;

        vm.ClearSelectionCommand.Execute(null);
        Check("untick all clears the selection", vm.SelectedCount == 0 && vm.Summary == "Nothing selected", vm.Summary);
        vm.Tiles[1].IsSelected = true;
        vm.OkCommand.Execute(null);
        Pump(500);
        Check("Ok closes the window as accepted with the ticked ids", window.Accepted && !window.IsVisible && vm.SelectedIds.SequenceEqual([options[1].Id]), "");

        var cancelVm = new ServicePickerViewModel(options, current);
        var cancelWindow = new ServicePickerWindow(cancelVm) { WindowStartupLocation = WindowStartupLocation.Manual, Left = 0, Top = 0, ShowInTaskbar = false, ShowActivated = false };
        cancelWindow.Show();
        Pump(500);
        cancelVm.CancelCommand.Execute(null);
        Pump(500);
        Check("Cancel closes without accepting", !cancelWindow.Accepted && !cancelWindow.IsVisible, "");
    }

    private static void PatientsSmoke()
    {
        var vm = Open<PatientsViewModel>(ModuleIds.Patients);
        Check("patients list loaded", vm.TotalCount >= 31, $"{vm.TotalCount} rows");
        Act(() => vm.NewRecordCommand.Execute(null));
        vm.PatientName = "Harness Patient";
        vm.DateOfBirth = new DateTime(1992, 3, 4);
        vm.Sex = "Female";
        Act(() => vm.SaveCommand.Execute(null), 2500);
        Check("patient created and found by code", vm.Id != 0 && !vm.IsError && vm.Items.Count == 1 && vm.SearchText == vm.PatientCode, $"{vm.PatientCode} items={vm.Items.Count}");
        Act(() => vm.DeleteCommand.Execute(null), 2500);
        Check("patient deleted", !vm.IsError && vm.Id == 0, vm.Message);
    }

    // ------------------------------------------------------------------ harness plumbing

    private static T Open<T>(int moduleId) where T : class
    {
        // Items and Item Locations are switched off in the legacy Modules table, so they are not in the menu; open them directly.
        var item = _mainVm.Menu.SelectMany(g => g.Items).FirstOrDefault(i => i.ModuleId == moduleId) ?? new DiagnosticLabs.Application.Menu.MenuItem(moduleId, "(hidden module {moduleId})");
        Console.WriteLine($"== {item.Name}");
        Act(() => _mainVm.OpenModuleCommand.Execute(item), 2500);
        return (T)_mainVm.CurrentPage;
    }

    private static void Check(string name, bool ok, string detail)
    {
        if (!ok)
            _failures++;
        Console.WriteLine($"  [{(ok ? "ok" : "FAIL")}] {name}{(string.IsNullOrEmpty(detail) ? "" : " - " + detail)}");
    }

    // Commands must start inside the dispatcher (as button clicks do) so awaits resume on the UI thread.
    private static void Act(Action action, int wait = 1500)
    {
        Application.Current.Dispatcher.BeginInvoke(action);
        Pump(wait);
    }

    private static void Pump(int milliseconds)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            frame.Continue = false;
        };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }

    private static void Snapshot(string file)
    {
        _main.UpdateLayout();
        var width = (int)_main.ActualWidth;
        var height = (int)_main.ActualHeight;
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(_main);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(_out, file));
        encoder.Save(stream);
    }

    private sealed class CapturingListener(List<string> sink) : TraceListener
    {
        public override void Write(string? message)
        {
        }

        public override void WriteLine(string? message)
        {
            if (!string.IsNullOrWhiteSpace(message))
                sink.Add(message);
        }
    }
}
