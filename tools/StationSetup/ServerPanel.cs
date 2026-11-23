using System.ComponentModel;
using System.Diagnostics;
using DiagnosticLabs.StationSetup.Core;

namespace DiagnosticLabs.StationSetup;

/// <summary>
/// The "Server" tab, used on the server PC: what stations type in (name, IP), whether the share is ready,
/// and the station password (show the saved one, or set a new one).
/// </summary>
internal sealed class ServerPanel : UserControl
{
    private static readonly string[] CheckNames =
    [
        $"Windows account {StationPaths.ShareAccount}",
        $"Share \\\\{Environment.MachineName}\\{StationPaths.ShareName}",
        "Program on the share",
        "Database settings on the share",
    ];

    private readonly bool _admin = StationAccount.IsAdministrator();
    private readonly StepList _checks = new(CheckNames) { Dock = DockStyle.Fill };
    private readonly TextBox _password = new() { Width = 220, ReadOnly = true, UseSystemPasswordChar = true, Font = new Font("Consolas", 12f) };
    private readonly CheckBox _show = new() { Text = "Show", AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly Button _copy = new() { Text = "Copy", AutoSize = true };
    private readonly Button _new = new() { Text = "Set a new password...", AutoSize = true };
    private readonly Label _passwordNote = new() { AutoSize = true, MaximumSize = new Size(700, 0) };

    public ServerPanel()
    {
        var ips = Network.LocalIPv4();
        var identity = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(700, 0),
            Margin = new Padding(0, 0, 0, 10),
            Text = "Use this tab on the server PC. On each station, type in as the server:\n" +
                   $"    {Environment.MachineName}" + (ips.Count > 0 ? "    or its IP address:  " + string.Join(",  ", ips) : "") + "\n" +
                   "The name is easier to read; use the IP address if a station cannot find the server by name (a fixed IP is then needed).",
        };

        var passwordRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
        passwordRow.Controls.AddRange([_password, _show, _copy, _new]);
        var passwordBox = new GroupBox { Text = $"Station password (Windows account {StationPaths.ShareAccount})", Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(10) };
        var passwordLayout = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        passwordLayout.Controls.AddRange([passwordRow, _passwordNote]);
        passwordBox.Controls.Add(passwordLayout);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1 };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(identity, 0, 0);
        layout.Controls.Add(passwordBox, 0, 1);
        layout.Controls.Add(_checks, 0, 2);

        var refresh = new Button { Text = "Check again", AutoSize = true, Anchor = AnchorStyles.Right };
        refresh.Click += (_, _) => RunChecks();
        layout.Controls.Add(refresh, 0, 3);
        Controls.Add(layout);

        _show.CheckedChanged += (_, _) => _password.UseSystemPasswordChar = !_show.Checked;
        _copy.Click += (_, _) => { if (_password.Text.Length > 0) Clipboard.SetText(_password.Text); };
        _new.Click += (_, _) => { if (_admin) SetNewPassword(); else RestartAsAdmin(); };

        if (!_admin)
        {
            _new.Text = "Restart as administrator";
            _show.Enabled = _copy.Enabled = false;
            _passwordNote.Text = "Showing or changing the station password needs administrator rights on this PC.";
        }
        else
        {
            LoadSavedPassword();
        }
        RunChecks();
    }

    private void LoadSavedPassword()
    {
        try
        {
            var saved = StationAccount.ReadCopy();
            _password.Text = saved ?? "";
            _show.Enabled = _copy.Enabled = saved is not null;
            _passwordNote.ForeColor = saved is null ? Color.DarkOrange : SystemColors.ControlText;
            _passwordNote.Text = saved is null
                ? "No password is saved on this server yet (Install-Server.ps1 shows it only once). Set a new one here; " +
                  "stations already set up must then run Station Setup again with it."
                : "Stations type this password in Station Setup. Keep it in the lab's password book as well.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.Cryptography.CryptographicException)
        {
            _passwordNote.ForeColor = Color.Firebrick;
            _passwordNote.Text = "The saved password could not be read: " + ex.Message;
        }
    }

    private void SetNewPassword()
    {
        var answer = MessageBox.Show(this,
            "A new random password will be set for the dlupdate account.\n\n" +
            "Every station that is already set up stops getting updates until Station Setup is run on it again with the new password " +
            "(Diagnostic Labs itself keeps working on those stations).\n\nSet a new password now?",
            "Station password", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
        if (answer != DialogResult.Yes) return;

        try
        {
            StationAccount.SetPassword(PasswordGenerator.New());
            SetupLog.Write("Station password changed on the server");
            LoadSavedPassword();
            _show.Checked = true;
            RunChecks();
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, "The password was not changed.\n\n" + ex.Message, "Station password", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void RestartAsAdmin()
    {
        try
        {
            Process.Start(new ProcessStartInfo(Environment.ProcessPath!, "--server") { UseShellExecute = true, Verb = "runas" });
            FindForm()?.Close();
        }
        catch (Win32Exception)
        {
            // The user said No to the Windows prompt.
        }
    }

    private void RunChecks()
    {
        try
        {
            var account = StationAccount.Read();
            if (account is null) _checks.Set(0, StepState.Failed, "Missing. Run Install-Server.ps1 on this PC.");
            else if (!account.Value.Enabled) _checks.Set(0, StepState.Failed, "Disabled. Set a new password above to enable it.");
            else if (!account.Value.NeverExpires) _checks.Set(0, StepState.Warning, "Its password expires. Set a new password above (it then never expires).");
            else _checks.Set(0, StepState.Ok, "Enabled, password never expires");
        }
        catch (Win32Exception ex) { _checks.Set(0, StepState.Warning, ex.Message); }

        var root = StationPaths.ShareRoot("localhost");
        try
        {
            if (!Directory.Exists(root)) { _checks.Set(1, StepState.Failed, "Not found. Run Install-Server.ps1 on this PC."); Skip(2); return; }
            _checks.Set(1, StepState.Ok, "Shared");

            var version = AppInstaller.ReadVersion(StationPaths.ShareCurrent("localhost"));
            _checks.Set(2, version is null ? StepState.Failed : StepState.Ok,
                version is null ? "No finished release in current (version.txt missing). Run Update-Server.ps1 with a release." : $"Version {version}");

            var config = StationPaths.ShareConfig("localhost");
            if (!File.Exists(config)) _checks.Set(3, StepState.Failed, "config\\appsettings.Local.json is missing. Run Install-Server.ps1 on this PC.");
            else
            {
                var db = DatabaseAddress.FromSettingsJson(File.ReadAllText(config));
                _checks.Set(3, db is null ? StepState.Failed : StepState.Ok,
                    db is null ? "No usable server name in the connection string." : $"Stations connect to {db.Host}, port {db.Port}");
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            _checks.Set(1, StepState.Warning, _admin ? ex.Message : "Could not look inside the share. Restart as administrator to check it.");
            Skip(2);
        }
    }

    private void Skip(int from)
    {
        for (var i = from; i < CheckNames.Length; i++) _checks.Set(i, StepState.Waiting, "");
    }
}
