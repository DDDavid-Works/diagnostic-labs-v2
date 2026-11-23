using System.Text.Json;
using DiagnosticLabs.StationSetup.Core;

namespace StationSetup.Tests;

public class StationPathsTests
{
    [Theory]
    [InlineData("SERVER", "SERVER")]
    [InlineData("  desktop-u6kn9af ", "desktop-u6kn9af")]
    [InlineData(@"\\SERVER\DiagnosticLabs\current", "SERVER")]
    [InlineData("192.168.1.20", "192.168.1.20")]
    [InlineData("//SERVER/share", "SERVER")]
    public void NormalizeHost_keeps_only_the_host(string typed, string expected) =>
        Assert.Equal(expected, StationPaths.NormalizeHost(typed));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(@"\\")]
    [InlineData("two words")]
    [InlineData("SERVER,1433")]
    public void NormalizeHost_rejects_what_is_not_a_host(string typed) =>
        Assert.Null(StationPaths.NormalizeHost(typed));

    [Fact]
    public void Share_paths_match_the_server_script()
    {
        Assert.Equal(@"\\SRV\DiagnosticLabs\current", StationPaths.ShareCurrent("SRV"));
        Assert.Equal(@"\\SRV\DiagnosticLabs\config\appsettings.Local.json", StationPaths.ShareConfig("SRV"));
        Assert.Equal(@"SRV\dlupdate", StationPaths.ShareUser("SRV"));
    }
}

public class LauncherFilesTests
{
    [Fact]
    public void Launcher_json_is_what_the_launcher_reads()
    {
        var json = LauncherFiles.LauncherJson(@"\\SRV\DiagnosticLabs\current");
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(@"\\SRV\DiagnosticLabs\current", doc.RootElement.GetProperty("SharePath").GetString());
    }

    [Fact]
    public void Write_puts_the_embedded_launcher_and_config_in_place_and_ReadHost_gets_the_server_back()
    {
        using var dir = new TempDir();
        LauncherFiles.Write(dir.Path, @"\\SRV\DiagnosticLabs\current");

        Assert.Equal(File.ReadAllBytes(Repo("tools", "deploy", LauncherFiles.ScriptName)), File.ReadAllBytes(Path.Combine(dir.Path, LauncherFiles.ScriptName)));
        Assert.Equal(File.ReadAllBytes(Repo("tools", "deploy", LauncherFiles.CmdName)), File.ReadAllBytes(Path.Combine(dir.Path, LauncherFiles.CmdName)));
        Assert.True(new FileInfo(Path.Combine(dir.Path, LauncherFiles.IconName)).Length > 0);
        Assert.Equal("SRV", LauncherFiles.ReadHost(dir.Path));
    }

    [Fact]
    public void ReadHost_is_null_without_a_launcher()
    {
        using var dir = new TempDir();
        Assert.Null(LauncherFiles.ReadHost(dir.Path));
    }

    private static string Repo(params string[] parts)
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "DiagnosticLabs.slnx"))) d = d.Parent;
        return Path.Combine([d!.FullName, .. parts]);
    }
}

public class DatabaseAddressTests
{
    [Theory]
    [InlineData("Server=DESKTOP-U6KN9AF,1433;Database=DiagnosticLabsV2;User Id=dl_app;Password=x;TrustServerCertificate=True", "DESKTOP-U6KN9AF", 1433)]
    [InlineData("Data Source=tcp:192.168.1.20,14330;Initial Catalog=X", "192.168.1.20", 14330)]
    [InlineData(@"Server=SRV\SQLEXPRESS;Database=X", "SRV", 1433)]
    public void Reads_host_and_port(string cs, string host, int port) =>
        Assert.Equal(new DatabaseAddress(host, port), DatabaseAddress.FromConnectionString(cs));

    [Theory]
    [InlineData("Server=.;Database=X")]
    [InlineData("Server=localhost,1433;Database=X")]
    [InlineData("Database=X")]
    public void Local_or_missing_server_is_not_usable_from_a_station(string cs) =>
        Assert.Null(DatabaseAddress.FromConnectionString(cs));

    [Fact]
    public void Reads_the_settings_file_the_server_script_writes()
    {
        var json = JsonSerializer.Serialize(new { ConnectionStrings = new { DiagnosticLabs = "Server=SRV,1433;Database=D;User Id=u;Password=p" } });
        Assert.Equal(new DatabaseAddress("SRV", 1433), DatabaseAddress.FromSettingsJson(json));
    }
}

public class PasswordGeneratorTests
{
    [Fact]
    public void Passwords_are_typeable_and_meet_windows_rules()
    {
        for (var i = 0; i < 200; i++)
        {
            var p = PasswordGenerator.New();
            Assert.Matches("^[A-Za-z2-9]{4}-[A-Za-z2-9]{4}-[A-Za-z2-9]{4}-[A-Za-z2-9]{4}$", p);
            Assert.DoesNotContain(p, c => "0O1lIoi".Contains(c));
            Assert.Contains(p, char.IsUpper);
            Assert.Contains(p, char.IsLower);
            Assert.Contains(p, char.IsDigit);
        }
        Assert.NotEqual(PasswordGenerator.New(), PasswordGenerator.New());
    }
}

public class AppInstallerTests
{
    [Fact]
    public void Mirror_copies_the_release_keeps_the_station_config_and_writes_the_version_last()
    {
        using var share = new TempDir();
        using var app = new TempDir();
        share.Write("DiagnosticLabs.Wpf.exe", "new exe");
        share.Write(@"runtimes\win\native.dll", "native");
        share.Write("version.txt", "1.0.4\r\n");
        share.Write(@"_server\schema.sql", "server only");
        share.Write("appsettings.Local.json", "never from the release");
        app.Write("appsettings.Local.json", "station's own");
        app.Write("old.dll", "dropped from the release");
        app.Write("version.txt", "1.0.3");

        var reports = new List<double>();
        var version = AppInstaller.Mirror(share.Path, app.Path, new SyncProgress(reports.Add));

        Assert.Equal("1.0.4", version);
        Assert.Equal("1.0.4", AppInstaller.ReadVersion(app.Path));
        Assert.Equal("new exe", app.Read("DiagnosticLabs.Wpf.exe"));
        Assert.Equal("native", app.Read(@"runtimes\win\native.dll"));
        Assert.Equal("station's own", app.Read("appsettings.Local.json"));
        Assert.False(File.Exists(Path.Combine(app.Path, "old.dll")));
        Assert.False(Directory.Exists(Path.Combine(app.Path, "_server")));
        Assert.Equal(1.0, reports[^1]);
    }

    [Fact]
    public void Mirror_refuses_a_release_without_version()
    {
        using var share = new TempDir();
        using var app = new TempDir();
        share.Write("DiagnosticLabs.Wpf.exe", "half copied");
        Assert.Throws<IOException>(() => AppInstaller.Mirror(share.Path, app.Path));
        Assert.Null(AppInstaller.ReadVersion(app.Path));
    }

    [Fact]
    public void SeedConfig_only_fills_a_missing_file()
    {
        using var share = new TempDir();
        using var app = new TempDir();
        share.Write("config.json", "from server");

        Assert.True(AppInstaller.SeedConfig(Path.Combine(share.Path, "config.json"), app.Path));
        Assert.Equal("from server", app.Read("appsettings.Local.json"));

        app.Write("appsettings.Local.json", "edited on the station");
        Assert.False(AppInstaller.SeedConfig(Path.Combine(share.Path, "config.json"), app.Path));
        Assert.Equal("edited on the station", app.Read("appsettings.Local.json"));
    }

    private sealed class SyncProgress(Action<double> report) : IProgress<double>
    {
        public void Report(double value) => report(value);
    }
}

public class ShareErrorTests
{
    [Theory]
    [InlineData(1326, "refused the password")]
    [InlineData(1219, "Sign out of Windows")]
    [InlineData(67, "no share named")]
    public void Windows_errors_become_instructions(int win32, string expected)
    {
        var ex = new IOException("raw", unchecked((int)0x80070000) | win32);
        Assert.Contains(expected, StationSetupRunner.ShareError(ex));
    }
}

internal sealed class TempDir : IDisposable
{
    public string Path { get; } = Directory.CreateTempSubdirectory("dl-setup-").FullName;

    public void Write(string relative, string text)
    {
        var file = System.IO.Path.Combine(Path, relative);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(file)!);
        File.WriteAllText(file, text);
    }

    public string Read(string relative) => File.ReadAllText(System.IO.Path.Combine(Path, relative));

    public void Dispose() => Directory.Delete(Path, recursive: true);
}
