namespace DiagnosticLabs.Wpf.Controls;

/// <summary>The last row of an entry dropdown ("Edit entries..."): choosing it opens the Entry Builder instead of picking a value.</summary>
public sealed class EditEntriesItem(string text)
{
    public string Text { get; } = text;

    public override string ToString() => Text;
}