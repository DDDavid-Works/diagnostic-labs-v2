using DiagnosticLabs.StationSetup.Core;

namespace DiagnosticLabs.StationSetup;

/// <summary>A list of named checks, each with a coloured mark and a one-line result.</summary>
internal sealed class StepList : ListView
{
    public StepList(IEnumerable<string> steps)
    {
        View = View.Details;
        FullRowSelect = true;
        HeaderStyle = ColumnHeaderStyle.Nonclickable;
        MultiSelect = false;
        ShowItemToolTips = true;
        Columns.Add("", 34);
        Columns.Add("Step", 270);
        Columns.Add("Result", 400);
        foreach (var step in steps)
            Items.Add(new ListViewItem(["", step, ""]));
        Resize += (_, _) => Columns[2].Width = Math.Max(150, ClientSize.Width - Columns[0].Width - Columns[1].Width - 4);
    }

    public void Reset()
    {
        foreach (ListViewItem item in Items) Set(item.Index, StepState.Waiting, "");
    }

    public void Set(int index, StepState state, string detail)
    {
        var item = Items[index];
        item.SubItems[0].Text = state switch
        {
            StepState.Running => "...",
            StepState.Ok => "✔",
            StepState.Warning => "!",
            StepState.Failed => "✖",
            _ => "",
        };
        item.UseItemStyleForSubItems = false;
        item.SubItems[0].ForeColor = state switch
        {
            StepState.Ok => Color.ForestGreen,
            StepState.Warning => Color.DarkOrange,
            StepState.Failed => Color.Firebrick,
            _ => SystemColors.GrayText,
        };
        item.SubItems[0].Font = new Font(Font, FontStyle.Bold);
        item.SubItems[2].Text = detail;
        item.SubItems[2].ForeColor = state == StepState.Failed ? Color.Firebrick : SystemColors.WindowText;
        item.ToolTipText = detail;
        if (state is StepState.Running or StepState.Failed) item.EnsureVisible();
    }
}
