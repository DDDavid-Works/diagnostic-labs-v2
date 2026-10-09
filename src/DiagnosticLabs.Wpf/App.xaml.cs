using System.IO;
using System.Windows;
using DiagnosticLabs.Application;
using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Infrastructure;
using DiagnosticLabs.Wpf.Services;
using DiagnosticLabs.Wpf.ViewModels;
using DiagnosticLabs.Wpf.Views;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

namespace DiagnosticLabs.Wpf;

public partial class App : System.Windows.Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            _host = BuildHost();
            await _host.StartAsync();

            DispatcherUnhandledException += (_, args) =>
            {
                _host.Services.GetRequiredService<ILogger<App>>()
                    .LogError(args.Exception, "Unhandled UI exception");
                MessageBox.Show(
                    "Something went wrong. The error was logged.",
                    "Diagnostic Labs",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                args.Handled = true;
            };

            ShowLogin();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application failed to start");
            MessageBox.Show($"The application could not start.\n\n{ex.Message}", "Diagnostic Labs", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync(TimeSpan.FromSeconds(3));
            _host.Dispose();
        }

        await Log.CloseAndFlushAsync();
        base.OnExit(e);
    }

    private static IHost BuildHost()
    {
        // Resolve config relative to the exe, not the working directory (shortcuts/installers differ).
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            ContentRootPath = AppContext.BaseDirectory,
        });

        // appsettings.Local.json is per-machine (gitignored): connection string, log level overrides, etc.
        builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false);

        // Environment variables win over every file (handy for CI and one-off runs against another database).
        builder.Configuration.AddEnvironmentVariables();

        var logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DiagnosticLabs",
            "logs",
            "log-.txt");

        builder.Services.AddSerilog((_, logger) => logger
            .ReadFrom.Configuration(builder.Configuration)
            .Enrich.FromLogContext()
            .WriteTo.File(logPath, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 30));

        builder.Services
            .AddApplication()
            .AddInfrastructure(builder.Configuration);

        builder.Services.AddSingleton<IServiceRunner, ServiceRunner>();
        builder.Services.AddSingleton<IDialogService, DialogService>();
        builder.Services.AddSingleton<IEntryBuilderDialog, EntryBuilderDialog>();
        builder.Services.AddSingleton<IPasswordChangeDialog, PasswordChangeDialog>();
        builder.Services.AddSingleton<IFileDialogService, FileDialogService>();
        builder.Services.AddSingleton<IServicePickerDialog, ServicePickerDialog>();
        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<PatientsViewModel>();
        builder.Services.AddTransient<CompaniesViewModel>();
        builder.Services.AddTransient<DepartmentsViewModel>();
        builder.Services.AddTransient<ServicesViewModel>();
        builder.Services.AddTransient<PackagesViewModel>();
        builder.Services.AddTransient<ItemsViewModel>();
        builder.Services.AddTransient<ItemLocationsViewModel>();
        builder.Services.AddTransient<DiscountsViewModel>();
        builder.Services.AddTransient<UsersViewModel>();
        builder.Services.AddTransient<CompanySetupViewModel>();
        builder.Services.AddTransient<LoginWindow>();
        builder.Services.AddTransient<MainViewModel>();
        builder.Services.AddTransient<MainWindow>();

        return builder.Build();
    }

    private void ShowLogin()
    {
        // The app uses explicit shutdown, so every way of leaving must end in Shutdown() unless the
        // user is just moving between the sign-in and main windows. Local flags are used on purpose:
        // WPF clears Application.MainWindow *before* a window's Closed event fires, so comparing it
        // there is always false and the process would stay alive after the window is closed.
        var login = _host!.Services.GetRequiredService<LoginWindow>();
        var signedIn = false;
        login.LoginSucceeded += (_, _) =>
        {
            signedIn = true;
            ContinueAfterSignIn();
        };
        login.Closed += (_, _) =>
        {
            if (!signedIn)
                Shutdown();
        };
        login.Show();
    }

    // A user holding a temporary password must replace it before seeing anything; backing out signs them out again.
    private void ContinueAfterSignIn()
    {
        var session = _host!.Services.GetRequiredService<ICurrentUserSession>();
        if (session.MustChangePassword && !_host.Services.GetRequiredService<IPasswordChangeDialog>().Show(forced: true))
        {
            session.SignOut();
            ShowLogin();
            return;
        }

        ShowMain();
    }

    private void ShowMain()
    {
        var main = _host!.Services.GetRequiredService<MainWindow>();
        var signedOut = false;
        MainWindow = main;
        main.SignedOut += (_, _) =>
        {
            signedOut = true;
            ShowLogin();
        };
        main.Closed += (_, _) =>
        {
            if (!signedOut)
                Shutdown();
        };
        main.Show();
    }
}
