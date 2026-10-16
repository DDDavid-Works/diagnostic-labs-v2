using System.Text.Json;
using System.Text.Json.Serialization;

namespace DiagnosticLabs.Application.LabResults.CustomForms;

/// <summary>The patient details a form can show in its header grid.</summary>
public enum HeaderField
{
    PatientCode,
    PatientName,
    Age,
    Sex,
    Company,
    DateRequested,
}

/// <summary>
/// Who signs the form. The roles map to the signatory columns of <c>LabReports</c> (Medical Technologist, second Medical Technologist, Pathologist), so a
/// form has at most two technologists and one pathologist.
/// </summary>
public enum SignatoryRole
{
    MedicalTechnologist,
    Pathologist,
}

public sealed record SignatorySlot(SignatoryRole Role);

/// <summary>The signatories (in print order), whether their licence numbers are printed (one switch for the whole form) and the Remarks box.</summary>
public sealed record FooterDefinition(IReadOnlyList<SignatorySlot> Signatories, bool ShowLicenseNumber, bool ShowRemarks);

public enum RowKind
{
    /// <summary>A test: its name, a result to enter and (when the group shows them) a reference/normal value.</summary>
    Test,

    /// <summary>A heading line inside the group (for example "DIFFERENTIAL COUNT") with nothing to enter.</summary>
    Section,
}

/// <summary>
/// One row of a group. <paramref name="Id"/> is stable for the life of the row (renaming never loses saved results, and it names the row's dropdown list
/// in the entry builder); <paramref name="Name"/> is the test name or the section heading.
/// </summary>
public sealed record RowDefinition(Guid Id, RowKind Kind, string Name);

/// <summary>
/// A column group of the body: Test Name, Result Value and (unless hidden) Reference/Normal Values, with an optional title above it.
/// The reference column is free text and is hidden per group (not shown and not printed; anything entered is kept).
/// </summary>
public sealed record GroupDefinition(Guid Id, string? Title, bool ShowReference, IReadOnlyList<RowDefinition> Rows);

/// <summary>A multiline text box under the groups (a label to type the text under).</summary>
public sealed record TextboxDefinition(Guid Id, string Label);

public sealed record BodyDefinition(IReadOnlyList<GroupDefinition> Groups, IReadOnlyList<TextboxDefinition> Textboxes)
{
    public static readonly BodyDefinition Empty = new([], []);

    public const int MaxGroups = 2;
    public const int MaxRowsPerGroup = 30;
    public const int MaxTextboxes = 3;
    public const int NameMaxLength = 100;
}

/// <summary>
/// One design of a custom form, saved as JSON in <c>CustomFormVersions.DefinitionJson</c>. The header is a grid of 3 columns and 2 rows (the 6 patient details) read row by row; an empty cell is
/// <c>null</c>. A version saved before the body existed has none (it reads as an empty body).
/// </summary>
public sealed record FormDefinition(IReadOnlyList<HeaderField?> Header, FooterDefinition Footer)
{
    public BodyDefinition Body { get; init; } = BodyDefinition.Empty;

    public const int HeaderColumns = 3;
    public const int HeaderRows = 2;
    public const int HeaderCellCount = HeaderColumns * HeaderRows;
    public const int MaxSignatories = 3;
    public const int MaxMedicalTechnologists = 2;
    public const int MaxPathologists = 1;

    /// <summary>What a new form starts with: patient code, age and sex on the first row, then patient name, date requested and company, one empty group, one technologist and one pathologist.</summary>
    public static FormDefinition NewForm() => new(
        [HeaderField.PatientCode, HeaderField.Age, HeaderField.Sex, HeaderField.PatientName, HeaderField.DateRequested, HeaderField.Company],
        new FooterDefinition([new SignatorySlot(SignatoryRole.MedicalTechnologist), new SignatorySlot(SignatoryRole.Pathologist)], true, true))
    {
        Body = new BodyDefinition([new GroupDefinition(Guid.NewGuid(), null, true, [])], []),
    };

    public static string DisplayName(HeaderField field) => field switch
    {
        HeaderField.PatientCode => "Patient Code",
        HeaderField.PatientName => "Patient Name",
        HeaderField.Age => "Age",
        HeaderField.Sex => "Sex/Gender",
        HeaderField.Company => "Company",
        HeaderField.DateRequested => "Date Requested",
        _ => field.ToString(),
    };

    public static string DisplayName(SignatoryRole role) => role switch
    {
        SignatoryRole.MedicalTechnologist => "Medical Technologist",
        SignatoryRole.Pathologist => "Pathologist",
        _ => role.ToString(),
    };

    /// <summary>The problems that stop a design from being saved (empty when it is fine).</summary>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (Header is null || Header.Count != HeaderCellCount)
        {
            errors.Add($"The header grid must have {HeaderCellCount} cells.");
        }
        else
        {
            if (Header.Any(f => f is { } field && !Enum.IsDefined(field)))
                errors.Add("The header has a field that does not exist.");

            var repeated = Header.Where(f => f is not null).GroupBy(f => f).Where(g => g.Count() > 1).Select(g => DisplayName(g.Key!.Value)).ToList();
            if (repeated.Count > 0)
                errors.Add($"A header field can only be used once: {string.Join(", ", repeated)}.");
        }

        errors.AddRange(ValidateBody());

        if (Footer?.Signatories is null)
        {
            errors.Add("The footer is missing.");
            return errors;
        }

        if (Footer.Signatories.Any(s => s is null || !Enum.IsDefined(s.Role)))
            errors.Add("The footer has a signatory that does not exist.");
        else
        {
            if (Footer.Signatories.Count > MaxSignatories)
                errors.Add($"A form can have up to {MaxSignatories} signatories.");

            if (Footer.Signatories.Count(s => s.Role == SignatoryRole.MedicalTechnologist) > MaxMedicalTechnologists)
                errors.Add($"A form can have up to {MaxMedicalTechnologists} medical technologists.");

            if (Footer.Signatories.Count(s => s.Role == SignatoryRole.Pathologist) > MaxPathologists)
                errors.Add($"A form can have only {MaxPathologists} pathologist.");
        }

        return errors;
    }

    private IEnumerable<string> ValidateBody()
    {
        var errors = new List<string>();
        var body = Body;
        if (body?.Groups is null || body.Textboxes is null)
        {
            errors.Add("The body is missing.");
            return errors;
        }

        if (body.Groups.Count > BodyDefinition.MaxGroups)
            errors.Add($"A form can have up to {BodyDefinition.MaxGroups} groups.");

        if (body.Textboxes.Count > BodyDefinition.MaxTextboxes)
            errors.Add($"A form can have up to {BodyDefinition.MaxTextboxes} text boxes.");

        var ids = new List<Guid>();
        for (var g = 0; g < body.Groups.Count; g++)
        {
            var group = body.Groups[g];
            var where = $"group {g + 1}";
            if (group?.Rows is null)
            {
                errors.Add($"Group {g + 1} is missing its rows.");
                continue;
            }

            ids.Add(group.Id);
            if ((group.Title?.Trim().Length ?? 0) > BodyDefinition.NameMaxLength)
                errors.Add($"The title of {where} can not be longer than {BodyDefinition.NameMaxLength} characters.");

            if (group.Rows.Count > BodyDefinition.MaxRowsPerGroup)
                errors.Add($"A group can have up to {BodyDefinition.MaxRowsPerGroup} rows ({where} has {group.Rows.Count}).");

            foreach (var row in group.Rows)
            {
                ids.Add(row?.Id ?? Guid.Empty);
                if (row is null || !Enum.IsDefined(row.Kind))
                    errors.Add($"A row in {where} does not exist.");
                else if ((row.Name?.Trim().Length ?? 0) == 0)
                    errors.Add(row.Kind == RowKind.Section ? $"A section heading in {where} needs a name." : $"Every row in {where} needs a test name.");
                else if (row.Name!.Trim().Length > BodyDefinition.NameMaxLength)
                    errors.Add($"A name in {where} can not be longer than {BodyDefinition.NameMaxLength} characters.");
            }
        }

        foreach (var box in body.Textboxes)
        {
            ids.Add(box?.Id ?? Guid.Empty);
            if ((box?.Label?.Trim().Length ?? 0) == 0)
                errors.Add("Every text box needs a name.");
            else if (box!.Label.Trim().Length > BodyDefinition.NameMaxLength)
                errors.Add($"A text box name can not be longer than {BodyDefinition.NameMaxLength} characters.");
        }

        if (ids.Any(id => id == Guid.Empty) || ids.Distinct().Count() != ids.Count)
            errors.Add("Every group, row and text box needs its own id.");

        return errors;
    }

    /// <summary>The same design with the names and titles trimmed (a blank title becomes none), as it is saved. Validate first.</summary>
    public FormDefinition Cleaned() => this with
    {
        Body = new BodyDefinition(
            [.. Body.Groups.Select(g => g with
            {
                Title = string.IsNullOrWhiteSpace(g.Title) ? null : g.Title.Trim(),
                Rows = [.. g.Rows.Select(r => r with { Name = r.Name.Trim() })],
            })],
            [.. Body.Textboxes.Select(t => t with { Label = t.Label.Trim() })]),
    };

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    public string ToJson() => JsonSerializer.Serialize(this, Json);

    public static FormDefinition FromJson(string json)
    {
        var design = JsonSerializer.Deserialize<FormDefinition>(json, Json) ?? throw new InvalidOperationException("The form design is empty.");

        // A version saved before the body existed has no "body" (or a null one): it reads as an empty body.
        design = design.Body is null ? design with { Body = BodyDefinition.Empty } : design;

        // An early design had a third, empty row of three cells in the header grid; it is left off.
        return design.Header is { Count: > HeaderCellCount } header && header.Skip(HeaderCellCount).All(c => c is null)
            ? design with { Header = [.. header.Take(HeaderCellCount)] }
            : design;
    }
}
