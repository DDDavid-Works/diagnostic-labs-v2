using System.Data.Common;
using System.Text.Json;

namespace DiagnosticLabs.StationSetup.Core;

/// <summary>The SQL Server host and port named in appsettings.Local.json (never its password).</summary>
internal sealed record DatabaseAddress(string Host, int Port)
{
    public const int DefaultPort = 1433;

    public static DatabaseAddress? FromSettingsJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("ConnectionStrings", out var cs) ||
            !cs.TryGetProperty("DiagnosticLabs", out var value) || value.GetString() is not { } text)
            return null;
        return FromConnectionString(text);
    }

    public static DatabaseAddress? FromConnectionString(string connectionString)
    {
        var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };
        object? server = null;
        foreach (var key in new[] { "Server", "Data Source", "Address", "Addr", "Network Address" })
            if (builder.TryGetValue(key, out server)) break;
        if (server?.ToString() is not { Length: > 0 } s) return null;

        if (s.StartsWith("tcp:", StringComparison.OrdinalIgnoreCase)) s = s[4..];
        var port = DefaultPort;
        var comma = s.IndexOf(',');
        if (comma >= 0)
        {
            if (!int.TryParse(s[(comma + 1)..].Trim(), out port)) port = DefaultPort;
            s = s[..comma];
        }
        // A named instance without a port goes through SQL Browser; the fixed port is what the server script sets up.
        var slash = s.IndexOf('\\');
        if (slash >= 0) s = s[..slash];
        s = s.Trim();
        if (s is "." or "(local)" or "localhost") return null;
        return s.Length == 0 ? null : new DatabaseAddress(s, port);
    }
}
