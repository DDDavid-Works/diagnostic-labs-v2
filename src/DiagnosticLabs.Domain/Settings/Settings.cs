using DiagnosticLabs.Domain.Common;

namespace DiagnosticLabs.Domain.Settings;

/// <summary>The laboratory's own details printed on reports (single logical row; the newest wins).</summary>
public class CompanySetup : AuditableEntity
{
    public string? CompanyName { get; set; }

    public string? SubCompanyName { get; set; }

    public string? Tagline { get; set; }

    public string? Address { get; set; }

    public string? ContactNumbers { get; set; }

    public string? Email { get; set; }

    /// <summary>Prefix used when building registration codes.</summary>
    public string? Code { get; set; }

    public byte[]? Logo { get; set; }
}

public enum LookupKind
{
    /// <summary>Prefilled value for a field (legacy DefaultValues).</summary>
    Default = 1,

    /// <summary>A choice in a single-line dropdown (legacy SingleLineEntries).</summary>
    SingleLine = 2,

    /// <summary>A reusable multi-line text template (legacy MultiLineEntries).</summary>
    MultiLine = 3,
}

/// <summary>Replaces DefaultValues, SingleLineEntries and MultiLineEntries.</summary>
public class LookupValue : ReferenceEntity
{
    public int? ModuleId { get; set; }

    public LookupKind Kind { get; set; }

    public string FieldName { get; set; } = string.Empty;

    public string? Title { get; set; }

    public string Value { get; set; } = string.Empty;

    /// <summary>The licence number of a signatory entry (medical technologist, pathologist...); empty for every other list.</summary>
    public string? LicenseNo { get; set; }
}

/// <summary>Per-module defaults blob (legacy LabResultsDefaults).</summary>
public class ModuleDefault : ReferenceEntity
{
    public int? ModuleId { get; set; }

    public string Defaults { get; set; } = string.Empty;
}

/// <summary>Atomic counter used to build registration codes (replaces the LatestCodeNumbers view).</summary>
public class CodeSequence
{
    public string Prefix { get; set; } = string.Empty;

    public long LastNumber { get; set; }

    public byte[] RowVersion { get; set; } = [];
}
