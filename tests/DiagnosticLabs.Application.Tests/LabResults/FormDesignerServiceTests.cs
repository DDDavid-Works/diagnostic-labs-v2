using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.LabResults.CustomForms;
using DiagnosticLabs.Domain.Catalog;
using DiagnosticLabs.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Application.Tests.LabResults;

public class FormDesignerServiceTests
{
    private readonly TestEnvironment _env = new();

    private void SignIn(bool create = true, bool edit = true, bool access = true) =>
        _env.Session.SignIn(new AuthenticatedUser(
            3, "u", "U", false, false, access ? [new ModulePermission(ModuleIds.FormDesigner, create, edit, false, false)] : []));

    /// <summary>A database with three services and the Clinical Microscopy module the designer copies its settings from.</summary>
    private async Task<(Infrastructure.Persistence.AppDbContext Db, FormDesignerService Service, long[] Services)> StartAsync()
    {
        var db = _env.CreateDb();
        db.Modules.Add(new Module { Id = ModuleIds.ClinicalMicroscopy, ModuleTypeId = 7, ModuleName = "Clinical Microscopy", Icon = "lab", IsActive = true });
        Service[] services =
        [
            new() { ServiceName = "Lipid Profile", Price = 100 },
            new() { ServiceName = "Thyroid Panel", Price = 200 },
            new() { ServiceName = "Old Service", Price = 5, IsActive = false },
        ];
        db.Services.AddRange(services);
        await db.SaveChangesAsync();
        SignIn();
        return (db, new FormDesignerService(db, _env.Session), [.. services.Select(s => s.Id)]);
    }

    private static FormInput NewInput(string name, long serviceId, FormDefinition? definition = null) =>
        new(0, name, serviceId, definition ?? FormDefinition.NewForm(), 0);

    [Fact]
    public async Task A_new_form_gets_its_own_module_linked_to_the_service_and_a_first_version()
    {
        var (db, service, ids) = await StartAsync();
        await using var _ = db;

        var design = FormDefinition.NewForm();
        var first = (await service.SaveAsync(NewInput("Lipid Profile", ids[0], design))).Value;
        var second = (await service.SaveAsync(NewInput("Thyroid Panel", ids[1]))).Value;

        Assert.Equal(ModuleIds.FirstCustomForm, first.ModuleId);
        Assert.Equal(ModuleIds.FirstCustomForm + 1, second.ModuleId);
        Assert.Equal(1, first.VersionNumber);
        Assert.Equal(ids[0], first.ServiceId);
        Assert.Equal(design.ToJson(), first.Definition.ToJson());

        var module = await db.Modules.SingleAsync(m => m.Id == first.ModuleId);
        Assert.Equal("Lipid Profile", module.ModuleName);
        Assert.Equal(ids[0], module.ServiceId);
        Assert.True(module.IsActive);
        Assert.Equal(7, module.ModuleTypeId); // the same menu group as the built-in result forms
        Assert.True(module.HasView && module.HasCreate && module.HasEdit && module.HasPrint && module.HasSetDefaults);
    }

    [Fact]
    public async Task The_designer_needs_its_own_permission()
    {
        var (db, service, ids) = await StartAsync();
        await using var _ = db;
        var saved = (await service.SaveAsync(NewInput("Lipid Profile", ids[0]))).Value;

        SignIn(access: false);
        Assert.Equal("Auth.Forbidden", (await service.ListAsync()).Error.Code);
        Assert.Equal("Auth.Forbidden", (await service.GetAsync(saved.Id)).Error.Code);
        Assert.Equal("Auth.Forbidden", (await service.SaveAsync(NewInput("Other", ids[1]))).Error.Code);

        // viewing is not creating, and creating is not editing
        SignIn(create: false, edit: false);
        Assert.True((await service.ListAsync()).IsSuccess);
        Assert.Equal("Auth.Forbidden", (await service.SaveAsync(NewInput("Other", ids[1]))).Error.Code);
        SignIn(create: true, edit: false);
        Assert.True((await service.SaveAsync(NewInput("Other", ids[1]))).IsSuccess);
        Assert.Equal("Auth.Forbidden", (await service.SaveAsync(new FormInput(saved.Id, "Renamed", ids[0], saved.Definition, 1))).Error.Code);
        Assert.Equal("Auth.Forbidden", (await service.SetActiveAsync(saved.Id, false, null)).Error.Code);
    }

    [Fact]
    public async Task A_service_can_belong_to_one_form_only_and_must_be_active()
    {
        var (db, service, ids) = await StartAsync();
        await using var _ = db;
        await service.SaveAsync(NewInput("Lipid Profile", ids[0]));

        Assert.True((await service.SaveAsync(NewInput("Another", ids[0]))).IsFailure);
        Assert.True((await service.SaveAsync(NewInput("Inactive", ids[2]))).IsFailure);
        Assert.True((await service.SaveAsync(NewInput("Missing", 9999))).IsFailure);

        // a built-in module that already uses the service blocks it as well
        db.Modules.Add(new Module { Id = 30 + 100, ModuleName = "Built in", ServiceId = ids[1] });
        await db.SaveChangesAsync();
        Assert.True((await service.SaveAsync(NewInput("Thyroid Panel", ids[1]))).IsFailure);
    }

    [Fact]
    public async Task The_service_list_leaves_out_services_that_are_taken_and_keeps_the_forms_own()
    {
        var (db, service, ids) = await StartAsync();
        await using var _ = db;
        var saved = (await service.SaveAsync(NewInput("Lipid Profile", ids[0]))).Value;

        Assert.Equal(["Thyroid Panel"], (await service.GetServiceChoicesAsync(null)).Value.Select(c => c.Name));
        Assert.Equal(["Lipid Profile", "Thyroid Panel"], (await service.GetServiceChoicesAsync(saved.Id)).Value.Select(c => c.Name));
    }

    [Fact]
    public async Task Names_are_required_and_unique()
    {
        var (db, service, ids) = await StartAsync();
        await using var _ = db;
        await service.SaveAsync(NewInput("Lipid Profile", ids[0]));

        Assert.True((await service.SaveAsync(NewInput("  ", ids[1]))).IsFailure);
        Assert.True((await service.SaveAsync(NewInput(new string('x', FormDesignerService.NameMaxLength + 1), ids[1]))).IsFailure);
        Assert.True((await service.SaveAsync(NewInput("Lipid Profile", ids[1]))).IsFailure);
        Assert.Equal("Thyroid", (await service.SaveAsync(NewInput("  Thyroid ", ids[1]))).Value.Name);
    }

    [Fact]
    public async Task A_changed_design_is_saved_as_the_next_version_and_an_unchanged_one_is_not()
    {
        var (db, service, ids) = await StartAsync();
        await using var _ = db;
        var v1 = (await service.SaveAsync(NewInput("Lipid Profile", ids[0]))).Value;

        var same = (await service.SaveAsync(new FormInput(v1.Id, "Lipid Profile", ids[0], v1.Definition, v1.VersionNumber))).Value;
        Assert.Equal(1, same.VersionNumber);

        var changed = v1.Definition with { Footer = v1.Definition.Footer with { ShowLicenseNumber = false } };
        var v2 = (await service.SaveAsync(new FormInput(v1.Id, "Lipid Profile", ids[0], changed, v1.VersionNumber))).Value;
        Assert.Equal(2, v2.VersionNumber);
        Assert.False(v2.Definition.Footer.ShowLicenseNumber);

        // the first version is kept as it was, so results saved with it still print the same
        var versions = await db.CustomFormVersions.OrderBy(v => v.VersionNumber).ToListAsync();
        Assert.Equal(2, versions.Count);
        Assert.True(FormDefinition.FromJson(versions[0].DefinitionJson).Footer.ShowLicenseNumber);
        Assert.False(FormDefinition.FromJson(versions[1].DefinitionJson).Footer.ShowLicenseNumber);

        // someone who opened version 1 can not overwrite version 2
        var stale = await service.SaveAsync(new FormInput(v1.Id, "Lipid Profile", ids[0], v1.Definition, 1));
        Assert.Equal("Form.Conflict", stale.Error.Code);
    }

    [Fact]
    public async Task Renaming_or_relinking_a_form_updates_its_module_and_keeps_the_version()
    {
        var (db, service, ids) = await StartAsync();
        await using var _ = db;
        var saved = (await service.SaveAsync(NewInput("Lipid Profile", ids[0]))).Value;

        var result = (await service.SaveAsync(new FormInput(saved.Id, "Lipids", ids[1], saved.Definition, 1))).Value;

        Assert.Equal(1, result.VersionNumber);
        var module = await db.Modules.SingleAsync(m => m.Id == saved.ModuleId);
        Assert.Equal("Lipids", module.ModuleName);
        Assert.Equal(ids[1], module.ServiceId);
    }

    [Fact]
    public async Task A_form_is_switched_off_not_deleted_and_frees_its_service()
    {
        var (db, service, ids) = await StartAsync();
        await using var _ = db;
        var saved = (await service.SaveAsync(NewInput("Lipid Profile", ids[0]))).Value;

        var off = (await service.SetActiveAsync(saved.Id, false, null)).Value;

        Assert.False(off.IsActive);
        Assert.Null(off.ServiceId);
        var module = await db.Modules.SingleAsync(m => m.Id == saved.ModuleId);
        Assert.False(module.IsActive);
        Assert.Null(module.ServiceId);
        Assert.Equal(1, await db.CustomForms.CountAsync());

        // the service can be used by a new form, a switched-off form can not be edited, and switching it on needs a free service
        var other = (await service.SaveAsync(NewInput("Replacement", ids[0]))).Value;
        Assert.True((await service.SaveAsync(new FormInput(saved.Id, "Lipid Profile", ids[1], saved.Definition, 1))).IsFailure);
        Assert.True((await service.SetActiveAsync(saved.Id, true, null)).IsFailure);
        Assert.True((await service.SetActiveAsync(saved.Id, true, ids[0])).IsFailure);

        var on = (await service.SetActiveAsync(saved.Id, true, ids[1])).Value;
        Assert.True(on.IsActive);
        Assert.Equal(ids[1], on.ServiceId);
        Assert.True((await db.Modules.SingleAsync(m => m.Id == saved.ModuleId)).IsActive);
        Assert.NotEqual(other.ModuleId, on.ModuleId);
    }

    [Fact]
    public async Task The_list_shows_active_forms_first_with_their_service_and_version()
    {
        var (db, service, ids) = await StartAsync();
        await using var _ = db;
        var a = (await service.SaveAsync(NewInput("Alpha", ids[0]))).Value;
        await service.SaveAsync(NewInput("Beta", ids[1]));
        await service.SetActiveAsync(a.Id, false, null);

        var list = (await service.ListAsync()).Value;

        Assert.Equal(["Beta", "Alpha"], list.Select(f => f.Name));
        Assert.Equal("Thyroid Panel", list[0].ServiceName);
        Assert.Null(list[1].ServiceName);
        Assert.All(list, f => Assert.Equal(1, f.VersionNumber));
    }

    [Fact]
    public async Task Creating_a_form_without_the_result_modules_in_the_database_is_refused()
    {
        await using var db = _env.CreateDb();
        db.Services.Add(new Service { ServiceName = "Lipid Profile" });
        await db.SaveChangesAsync();
        SignIn();

        var result = await new FormDesignerService(db, _env.Session).SaveAsync(NewInput("Lipid Profile", db.Services.Single().Id));

        Assert.True(result.IsFailure);
        Assert.Empty(db.CustomForms);
    }

    public static IEnumerable<object[]> InvalidDesigns()
    {
        var good = FormDefinition.NewForm();
        HeaderField?[] Cells(params HeaderField?[] cells) => [.. cells, .. new HeaderField?[FormDefinition.HeaderCellCount - cells.Length]];

        yield return [good with { Header = Cells(HeaderField.Age, HeaderField.Age) }];
        yield return [good with { Header = [HeaderField.Age] }];
        yield return [good with { Header = [.. Cells(HeaderField.Age), null] }];
        yield return [good with { Header = Cells((HeaderField)99) }];
        yield return [good with { Footer = good.Footer with { Signatories = [.. Enumerable.Repeat(new SignatorySlot(SignatoryRole.MedicalTechnologist), 3)] } }];
        yield return [good with { Footer = good.Footer with { Signatories = [new(SignatoryRole.Pathologist), new(SignatoryRole.Pathologist)] } }];
        yield return [good with { Footer = good.Footer with { Signatories = [new(SignatoryRole.MedicalTechnologist), new(SignatoryRole.MedicalTechnologist), new(SignatoryRole.Pathologist), new(SignatoryRole.Pathologist)] } }];
        yield return [good with { Footer = good.Footer with { Signatories = [new((SignatoryRole)9)] } }];
    }

    [Theory]
    [MemberData(nameof(InvalidDesigns))]
    public async Task A_design_that_breaks_the_rules_is_not_saved(FormDefinition design)
    {
        var (db, service, ids) = await StartAsync();
        await using var _ = db;

        Assert.NotEmpty(design.Validate());
        Assert.True((await service.SaveAsync(NewInput("Lipid Profile", ids[0], design))).IsFailure);
        Assert.Empty(db.CustomForms);
    }

    [Fact]
    public void The_largest_allowed_design_and_an_empty_one_are_valid()
    {
        var full = new FormDefinition(
            [HeaderField.PatientCode, HeaderField.PatientName, HeaderField.Age, HeaderField.Sex, HeaderField.Company, HeaderField.DateRequested],
            new FooterDefinition([new(SignatoryRole.MedicalTechnologist), new(SignatoryRole.MedicalTechnologist), new(SignatoryRole.Pathologist)], false, false));
        var empty = new FormDefinition(new HeaderField?[FormDefinition.HeaderCellCount], new FooterDefinition([], true, true));

        Assert.Empty(full.Validate());
        Assert.Empty(empty.Validate());
        Assert.Empty(FormDefinition.NewForm().Validate());
    }

    [Fact]
    public void A_design_survives_the_json_round_trip_with_empty_cells_and_readable_names()
    {
        var design = FormDefinition.NewForm();

        var json = design.ToJson();
        var back = FormDefinition.FromJson(json);

        Assert.Contains("\"PatientName\"", json);
        Assert.Contains("\"MedicalTechnologist\"", json);
        Assert.Equal(json, back.ToJson());
        Assert.Equal(design.Header, back.Header);
        Assert.Equal(FormDefinition.HeaderCellCount, back.Header.Count);
        Assert.Equal(HeaderField.Company, back.Header[5]);
        Assert.Equal([SignatoryRole.MedicalTechnologist, SignatoryRole.Pathologist], back.Footer.Signatories.Select(s => s.Role));
    }

    [Fact]
    public void Every_header_field_and_role_has_a_display_name()
    {
        var fields = Enum.GetValues<HeaderField>().Select(FormDefinition.DisplayName).ToList();
        Assert.All(fields, name => Assert.False(string.IsNullOrWhiteSpace(name)));
        Assert.Equal(fields.Count, fields.Distinct().Count());
        Assert.Equal(["Medical Technologist", "Pathologist"], Enum.GetValues<SignatoryRole>().Select(FormDefinition.DisplayName));
    }
}
