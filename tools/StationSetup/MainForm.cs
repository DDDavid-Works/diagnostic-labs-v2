namespace DiagnosticLabs.StationSetup;

internal sealed class MainForm : Form
{
    public MainForm(bool openServerTab)
    {
        Text = "Diagnostic Labs - Station Setup";
        Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!);
        Font = new Font("Segoe UI", 9.75f);
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(760, 620);
        MinimumSize = new Size(640, 560);
        AutoScaleMode = AutoScaleMode.Dpi;

        var tabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(14, 5) };
        var station = new TabPage("This station") { Padding = new Padding(12) };
        station.Controls.Add(new StationPanel { Dock = DockStyle.Fill });
        var server = new TabPage("Server") { Padding = new Padding(12) };
        server.Controls.Add(new ServerPanel { Dock = DockStyle.Fill });
        tabs.TabPages.Add(station);
        tabs.TabPages.Add(server);
        if (openServerTab) tabs.SelectedTab = server;
        Controls.Add(tabs);
    }
}
