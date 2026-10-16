using DiagnosticLabs.Domain.Common;
using DiagnosticLabs.Domain.Identity;

namespace DiagnosticLabs.Domain.Lab;

/// <summary>
/// A result form designed by the client in the Custom Lab Form designer. Every form is also a <see cref="Module"/> (a menu entry and permission target, ids start at
/// 1000), and the billable service it belongs to is linked through <c>Modules.ServiceId</c>, as for the built-in forms. A form is never deleted:
/// switching <see cref="ReferenceEntity.IsActive"/> off hides it while its saved results stay printable.
/// </summary>
public class CustomForm : ReferenceEntity
{
    public string Name { get; set; } = string.Empty;

    public int ModuleId { get; set; }

    public Module Module { get; set; } = null!;

    public List<CustomFormVersion> Versions { get; set; } = [];
}

/// <summary>
/// One saved design of a <see cref="CustomForm"/>. Saving a changed design adds the next version; a version is never edited afterwards, so a result
/// saved with it always opens and prints as it was. The current design is the version with the highest number.
/// </summary>
public class CustomFormVersion : AuditableEntity
{
    public long CustomFormId { get; set; }

    public CustomForm CustomForm { get; set; } = null!;

    public int VersionNumber { get; set; }

    /// <summary>The design as JSON (header grid, body groups and rows, textboxes, footer); every group, row and textbox carries a stable id.</summary>
    public string DefinitionJson { get; set; } = string.Empty;
}
