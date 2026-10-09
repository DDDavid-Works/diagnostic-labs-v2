namespace DiagnosticLabs.Domain.Identity;

/// <summary>A menu group (e.g. Registration, Lab Results) that modules belong to.</summary>
public class ModuleType
{
    public int Id { get; set; }

    public string ModuleTypeName { get; set; } = string.Empty;

    public string Icon { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public bool IsActive { get; set; }

    public bool IsAdmin { get; set; }
}
