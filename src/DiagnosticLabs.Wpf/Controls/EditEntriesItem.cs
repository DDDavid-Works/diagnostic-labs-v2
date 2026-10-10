namespace DiagnosticLabs.Wpf.Controls;

/// <summary>The last row of an entry dropdown ("Edit entries..."): choosing it opens the Entry Builder instead of picking a value.</summary>
public sealed class EditEntriesItem(string text)
{
    public string Text { get; } = text;

    // Empty on purpose: a combo box matches typed letters against this text, and typing "E" must not pick the row and open the builder.
    public override string ToString() => string.Empty;
}