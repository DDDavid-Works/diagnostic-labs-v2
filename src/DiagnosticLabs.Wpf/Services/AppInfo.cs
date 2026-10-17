using System.Reflection;
using DiagnosticLabs.Application.Stations;

namespace DiagnosticLabs.Wpf.Services;

/// <summary>The release number comes from the build (<c>Publish-Release.ps1 -Version</c>), the same text as the server's <c>version.txt</c>.</summary>
public sealed class AppInfo : IAppInfo
{
    public string Version { get; } = ReadVersion();

    public string StationName => Environment.MachineName;

    private static string ReadVersion()
    {
        var text = typeof(AppInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0";

        // The SDK may append "+<commit>" to the version; the launcher and the server only know the part before it.
        var plus = text.IndexOf('+');
        return plus >= 0 ? text[..plus] : text;
    }
}
