using System.Diagnostics;
using DiagnosticLabs.StationSetup.Core;

namespace DiagnosticLabs.StationSetup;

/// <summary>The "This station" tab: server and password in, one button, every step shown with its result.</summary>
internal sealed class StationPanel : UserControl
{
    private readonly TextBox _server = new() { Width = 260 };
    private readonly TextBox _password = new() { Width = 260, UseSystemPasswordChar = true };
    private readonly CheckBox _showPassword = new() { Text = "Show", AutoSize = true };
    private readonly Button _run = new() { Text = "Set up this PC", AutoSize = true, Padding = new Padding(10, 2, 10, 2) };
    private readonly StepList _steps = new(StationSetupRunner.Steps) { Dock = DockStyle.Fill };
    private readonly ProgressBar _copy = new() { Dock = DockStyle.Fill, Maximum = 1000, Height = 14 };
    private readonly Label _status = new() { AutoSize = true, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
    private readonly Button _open = new() { Text = "Open Diagnostic Labs", AutoSize = true, Enabled = false, Padding = new Padding(10, 2, 10, 2) };
    private readonly Button _log = new() { Text = "Open log", AutoSize = true };

    public StationPanel()
    {
        _server.Text = LauncherFiles.ReadHost(StationPaths.LauncherDir) ?? "";
        _showPassword.CheckedChanged += (_, _) => _password.UseSystemPasswordChar = !_showPassword.Checked;
        _run.Click += async (_, _) => await RunAsync();
        _open.Click += (_, _) => OpenApp();
        _log.Click += (_, _) => OpenLog();

        var intro = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(700, 0),
            Margin = new Padding(0, 0, 0, 10),
            Text = "Run this while signed in to Windows as the person who will use Diagnostic Labs on this PC. " +
                   "If several Windows users share the PC, run it once for each of them. No administrator rights are needed.\n\n" +
                   "Server: the server PC's name or IP address. Station password: the password of the dlupdate account " +
                   "(on the server, open this program's Server tab to see it).",
        };

        var fields = new TableLayoutPanel { AutoSize = true, ColumnCount = 3, Dock = DockStyle.Top };
        fields.Controls.Add(new Label { Text = "Server name or IP", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        fields.Controls.Add(_server, 1, 0);
        fields.Controls.Add(new Label { Text = "Station password", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        fields.Controls.Add(_password, 1, 1);
        fields.Controls.Add(_showPassword, 2, 1);
        fields.Controls.Add(_run, 1, 2);

        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, AutoSize = true };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        footer.Controls.Add(_status, 0, 0);
        footer.Controls.Add(_log, 1, 0);
        footer.Controls.Add(_open, 2, 0);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1 };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(intro, 0, 0);
        layout.Controls.Add(fields, 0, 1);
        layout.Controls.Add(_steps, 0, 2);
        layout.Controls.Add(_copy, 0, 3);
        layout.Controls.Add(footer, 0, 4);
        Controls.Add(layout);
    }

    private async Task RunAsync()
    {
        var host = StationPaths.NormalizeHost(_server.Text);
        if (host is null) { Warn("Type the server's name or IP address.", _server); return; }
        if (_password.Text.Length == 0) { Warn("Type the station password.", _password); return; }
        _server.Text = host;

        _run.Enabled = _open.Enabled = _server.Enabled = _password.Enabled = false;
        _steps.Reset();
        _copy.Value = 0;
        _status.ForeColor = SystemColors.ControlText;
        _status.Text = "Setting up...";

        var progress = new Progress<double>(p => _copy.Value = (int)Math.Clamp(p * 1000, 0, 1000));
        var runner = new StationSetupRunner(host, _password.Text, r => BeginInvoke(() => _steps.Set(r.Index, r.State, r.Detail)), progress);
        bool ok;
        try { ok = await runner.RunAsync(); }
        catch (Exception ex)
        {
            ok = false;
            SetupLog.Write("Setup stopped: " + ex);
        }

        _run.Enabled = _server.Enabled = _password.Enabled = true;
        _open.Enabled = ok;
        _status.ForeColor = ok ? Color.ForestGreen : Color.Firebrick;
        _status.Text = ok
            ? "This PC is ready. Use the Diagnostic Labs shortcut on the desktop."
            : "Setup stopped. Fix the step marked in red, then press Set up this PC again.";
    }

    private void Warn(string text, Control focus)
    {
        MessageBox.Show(this, text, "Station Setup", MessageBoxButtons.OK, MessageBoxIcon.Information);
        focus.Focus();
    }

    private static void OpenApp() =>
        Process.Start(new ProcessStartInfo(Path.Combine(StationPaths.LauncherDir, LauncherFiles.CmdName))
        {
            UseShellExecute = true,
            WorkingDirectory = StationPaths.LauncherDir,
            WindowStyle = ProcessWindowStyle.Minimized,
        });

    private void OpenLog()
    {
        if (File.Exists(StationPaths.LogFile)) Process.Start(new ProcessStartInfo(StationPaths.LogFile) { UseShellExecute = true });
        else MessageBox.Show(this, "There is no log yet on this PC.", "Station Setup");
    }
}
