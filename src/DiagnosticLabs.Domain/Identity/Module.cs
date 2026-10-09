namespace DiagnosticLabs.Domain.Identity;

/// <summary>A menu entry / permission target (a screen) and the actions it supports.</summary>
public class Module
{
    public int Id { get; set; }

    public int ModuleTypeId { get; set; }

    public ModuleType ModuleType { get; set; } = null!;

    public string ModuleName { get; set; } = string.Empty;

    public bool HasView { get; set; }

    public bool HasCreate { get; set; }

    public bool HasEdit { get; set; }

    public bool HasDelete { get; set; }

    public bool HasSearch { get; set; }

    public bool HasPrint { get; set; }

    public bool HasShowList { get; set; }

    public bool HasSetDefaults { get; set; }

    public string Icon { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public bool IsActive { get; set; }

    public long? ServiceId { get; set; }
}
