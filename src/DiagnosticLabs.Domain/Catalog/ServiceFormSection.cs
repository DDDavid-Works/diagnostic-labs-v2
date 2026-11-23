namespace DiagnosticLabs.Domain.Catalog;

/// <summary>A part of a result form that is charged as a service of its own (the sections of the Physical Examination sheet).</summary>
public enum FormSection
{
    Cbc,
    Urinalysis,
    Fecalysis,
}

/// <summary>
/// A service whose result can be entered in one section of a result form (module), next to (or instead of) the form linked through
/// <c>Modules.ServiceId</c>. Complete Blood Count is entered only in the CBC section of the Physical Examination sheet; Urinalysis and
/// Stool/Fecalysis can be entered in their own forms or in their section of that sheet (the home screen asks which until a result is saved).
/// </summary>
public class ServiceFormSection
{
    public int Id { get; set; }

    public long ServiceId { get; set; }

    public Service Service { get; set; } = null!;

    public int ModuleId { get; set; }

    public FormSection Section { get; set; }
}
