using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.LabResults.CustomForms;
using DiagnosticLabs.Domain.Catalog;
using DiagnosticLabs.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Application.Tests.LabResults;

/// <summary>The body of a custom form: up to 2 groups of up to 30 rows (tests and section headings) and up to 3 text boxes.</summary>
public class FormBodyTests
{
    private readonly TestEnvironment _env = new();

    private static RowDefinition Test(string name) => new(Guid.NewGuid(), RowKind.Test, name);

    private static RowDefinition Section(string name) => new(Guid.NewGuid(), RowKind.Section, name);

    private static GroupDefinition Group(string? title = null, bool showReference = true, params RowDefinition[] rows) => new(Guid.NewGuid(), title, showReference, rows);

    private static TextboxDefinition Box(string label) => new(Guid.NewGuid(), label);

    private static FormDefinition With(BodyDefinition body) => FormDefinition.NewForm() with { Body = body };

    private static FormDefinition With(params GroupDefinition[] groups) => With(new BodyDefinition(groups, []));

    [Fact]
    public void A_new_form_starts_with_one_empty_group_that_shows_the_reference_column()
    {
        var body = FormDefinition.NewForm().Body;

        var group = Assert.Single(body.Groups);
        Assert.True(group.ShowReference);
        Assert.Empty(group.Rows);
        Assert.Empty(body.Textboxes);
        Assert.Empty(FormDefinition.NewForm().Validate());
    }

    [Fact]
    public void The_largest_body_and_an_empty_one_are_valid()
    {
        GroupDefinition Full() => Group("Group", true, [.. Enumerable.Range(1, BodyDefinition.MaxRowsPerGroup).Select(i => i % 5 == 0 ? Section($"Section {i}") : Test($"Test {i}"))]);
        var full = With(new BodyDefinition([Full(), Full()], [Box("Findings"), Box("Impression"), Box("Advice")]));

        Assert.Empty(full.Validate());
        Assert.Empty(With(BodyDefinition.Empty).Validate());
        Assert.Empty(With(Group(), Group(showReference: false)).Validate());
    }

    public static IEnumerable<object[]> InvalidBodies()
    {
        var thirtyOne = Enumerable.Range(1, BodyDefinition.MaxRowsPerGroup + 1).Select(i => Test($"Test {i}")).ToArray();
        var sharedId = Guid.NewGuid();

        yield return [With(Group(), Group(), Group())];
        yield return [With(Group(null, true, thirtyOne))];
        yield return [With(new BodyDefinition([Group()], [Box("a"), Box("b"), Box("c"), Box("d")]))];
        yield return [With(Group(null, true, Test("  ")))];
        yield return [With(Group(null, true, Test("")))];
        yield return [With(Group(null, true, Section(" ")))];
        yield return [With(new BodyDefinition([Group()], [Box(" ")]))];
        yield return [With(Group(null, true, Test(new string('x', BodyDefinition.NameMaxLength + 1))))];
        yield return [With(Group(new string('x', BodyDefinition.NameMaxLength + 1)))];
        yield return [With(new BodyDefinition([Group()], [Box(new string('x', BodyDefinition.NameMaxLength + 1))]))];
        yield return [With(Group(null, true, new RowDefinition(sharedId, RowKind.Test, "A"), new RowDefinition(sharedId, RowKind.Test, "B")))];
        yield return [With(Group(null, true, new RowDefinition(Guid.Empty, RowKind.Test, "A")))];
        yield return [With(Group(null, true, new RowDefinition(Guid.NewGuid(), (RowKind)9, "A")))];
        yield return [With(new BodyDefinition([Group() with { Id = Guid.Empty }], []))];
        yield return [With(new BodyDefinition([Group(), Group() with { Rows = [Test("A")] }], [new TextboxDefinition(sharedId, "x"), new TextboxDefinition(sharedId, "y")]))];
    }

    [Theory]
    [MemberData(nameof(InvalidBodies))]
    public void A_body_that_breaks_the_rules_is_not_valid(FormDefinition design) => Assert.NotEmpty(design.Validate());

    [Fact]
    public void The_same_id_in_two_groups_is_refused_but_blank_group_titles_are_fine()
    {
        var row = Test("A");
        Assert.NotEmpty(With(Group(null, true, row), Group(null, true, row)).Validate());
        Assert.Empty(With(Group("   ", true, Test("A")), Group(null, true, Test("A"))).Validate()); // the same test name twice is allowed
    }

    [Fact]
    public void Cleaning_trims_names_and_drops_blank_titles_but_keeps_ids_and_order()
    {
        var first = Test("  Hemoglobin ");
        var second = Section(" CBC ");
        var design = With(new BodyDefinition([Group("  Left  ", true, first, second), Group("   ", false)], [Box("  Findings ")]));

        var cleaned = design.Cleaned();

        Assert.Equal("Left", cleaned.Body.Groups[0].Title);
        Assert.Null(cleaned.Body.Groups[1].Title);
        Assert.False(cleaned.Body.Groups[1].ShowReference);
        Assert.Equal(["Hemoglobin", "CBC"], cleaned.Body.Groups[0].Rows.Select(r => r.Name));
        Assert.Equal([first.Id, second.Id], cleaned.Body.Groups[0].Rows.Select(r => r.Id));
        Assert.Equal("Findings", cleaned.Body.Textboxes[0].Label);
    }

    [Fact]
    public void The_body_survives_the_json_round_trip_with_ids_kinds_and_order()
    {
        var rows = new[] { Test("Hemoglobin"), Section("DIFFERENTIAL"), Test("Neutrophils") };
        var design = With(new BodyDefinition([Group("Left", true, rows), Group(null, false, Test("Glucose"))], [Box("Findings")]));

        var json = design.ToJson();
        var back = FormDefinition.FromJson(json);

        Assert.Equal(json, back.ToJson());
        Assert.Contains("\"Section\"", json);
        Assert.Equal(design.Body.Groups[0].Id, back.Body.Groups[0].Id);
        Assert.Equal(rows.Select(r => r.Id), back.Body.Groups[0].Rows.Select(r => r.Id));
        Assert.Equal([RowKind.Test, RowKind.Section, RowKind.Test], back.Body.Groups[0].Rows.Select(r => r.Kind));
        Assert.False(back.Body.Groups[1].ShowReference);
        Assert.Equal(design.Body.Textboxes[0].Id, back.Body.Textboxes[0].Id);
    }

    [Fact]
    public void A_version_saved_before_the_body_existed_reads_as_an_empty_body()
    {
        const string old = """{"header":["PatientCode",null,null,null,null,null,null,null,null],"footer":{"signatories":[{"role":"Pathologist"}],"showLicenseNumber":true,"showRemarks":false}}""";

        var design = FormDefinition.FromJson(old);
        var withNull = FormDefinition.FromJson(old.Replace("\"footer\"", "\"body\":null,\"footer\""));

        Assert.Equal(FormDefinition.HeaderCellCount, design.Header.Count); // the early 3 x 3 header lost its empty third row
        Assert.Equal(HeaderField.PatientCode, design.Header[0]);
        Assert.Equal(BodyDefinition.Empty, design.Body);
        Assert.Equal(BodyDefinition.Empty, withNull.Body);
        Assert.Empty(design.Validate());
    }

    // ---------------------------------------------------------------- saving through the service

    private async Task<(Infrastructure.Persistence.AppDbContext Db, FormDesignerService Service, long ServiceId)> StartAsync()
    {
        var db = _env.CreateDb();
        db.Modules.Add(new Module { Id = ModuleIds.ClinicalMicroscopy, ModuleTypeId = 7, ModuleName = "Clinical Microscopy", Icon = "lab", IsActive = true });
        var service = new Service { ServiceName = "CBC", Price = 100 };
        db.Services.Add(service);
        await db.SaveChangesAsync();
        _env.Session.SignIn(new AuthenticatedUser(3, "u", "U", false, false, [new ModulePermission(ModuleIds.FormDesigner, true, true, false, false)]));
        return (db, new FormDesignerService(db, _env.Session), service.Id);
    }

    [Fact]
    public async Task A_saved_body_comes_back_trimmed_with_its_rows_in_order()
    {
        var (db, service, serviceId) = await StartAsync();
        await using var _ = db;
        var rows = new[] { Test(" Hemoglobin "), Section("DIFFERENTIAL"), Test("Neutrophils") };

        var saved = (await service.SaveAsync(new FormInput(0, "CBC", serviceId, With(Group(" Left ", true, rows)), 0))).Value;

        var group = Assert.Single(saved.Definition.Body.Groups);
        Assert.Equal("Left", group.Title);
        Assert.Equal(["Hemoglobin", "DIFFERENTIAL", "Neutrophils"], group.Rows.Select(r => r.Name));
        Assert.Equal(rows.Select(r => r.Id), group.Rows.Select(r => r.Id));
    }

    [Fact]
    public async Task Renaming_reordering_or_adding_a_row_makes_a_new_version_and_keeps_the_row_ids()
    {
        var (db, service, serviceId) = await StartAsync();
        await using var _ = db;
        var a = Test("Hemoglobin");
        var b = Test("Hematocrit");
        var v1 = (await service.SaveAsync(new FormInput(0, "CBC", serviceId, With(Group(null, true, a, b)), 0))).Value;

        var reordered = v1.Definition with { Body = new BodyDefinition([v1.Definition.Body.Groups[0] with { Rows = [b, a] }], []) };
        var v2 = (await service.SaveAsync(new FormInput(v1.Id, "CBC", serviceId, reordered, 1))).Value;
        Assert.Equal(2, v2.VersionNumber);
        Assert.Equal([b.Id, a.Id], v2.Definition.Body.Groups[0].Rows.Select(r => r.Id));

        var renamed = v2.Definition with { Body = new BodyDefinition([v2.Definition.Body.Groups[0] with { Rows = [b, a with { Name = "Hgb" }] }], []) };
        var v3 = (await service.SaveAsync(new FormInput(v1.Id, "CBC", serviceId, renamed, 2))).Value;
        Assert.Equal(3, v3.VersionNumber);
        Assert.Equal(a.Id, v3.Definition.Body.Groups[0].Rows[1].Id);

        var same = (await service.SaveAsync(new FormInput(v1.Id, "CBC", serviceId, v3.Definition, 3))).Value;
        Assert.Equal(3, same.VersionNumber);

        // version 1 still has the original order and name
        var first = FormDefinition.FromJson((await db.CustomFormVersions.SingleAsync(x => x.VersionNumber == 1)).DefinitionJson);
        Assert.Equal(["Hemoglobin", "Hematocrit"], first.Body.Groups[0].Rows.Select(r => r.Name));
    }

    [Fact]
    public async Task Too_many_rows_or_a_blank_name_stop_the_save()
    {
        var (db, service, serviceId) = await StartAsync();
        await using var _ = db;
        var tooMany = Group(null, true, [.. Enumerable.Range(1, BodyDefinition.MaxRowsPerGroup + 1).Select(i => Test($"T{i}"))]);

        var result = await service.SaveAsync(new FormInput(0, "CBC", serviceId, With(tooMany), 0));
        Assert.True(result.IsFailure);
        Assert.Contains("up to 30 rows", result.Error.Message);

        var blank = await service.SaveAsync(new FormInput(0, "CBC", serviceId, With(Group(null, true, Test(" "))), 0));
        Assert.Contains("needs a test name", blank.Error.Message);
        Assert.Empty(db.CustomForms);
    }
}
