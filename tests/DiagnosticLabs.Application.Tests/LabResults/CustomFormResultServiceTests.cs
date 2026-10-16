using System.Text.Json;
using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.LabResults;
using DiagnosticLabs.Application.LabResults.CustomForms;
using DiagnosticLabs.Domain.Identity;
using DiagnosticLabs.Domain.Lab;
using DiagnosticLabs.Domain.Lab.Reports;
using DiagnosticLabs.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Application.Tests.LabResults;

/// <summary>Saving, reading and printing the results of forms made in the Custom Lab Form designer.</summary>
public class CustomFormResultServiceTests
{
    private const int FormA = ModuleIds.FirstCustomForm;
    private const int FormB = ModuleIds.FirstCustomForm + 1;

    private readonly TestEnvironment _env = new();

    private static readonly Guid HemoglobinId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid HeadingId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid GlucoseId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid FindingsId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private static FormDefinition Design() => FormDefinition.NewForm() with
    {
        Footer = new FooterDefinition([new(SignatoryRole.MedicalTechnologist), new(SignatoryRole.MedicalTechnologist), new(SignatoryRole.Pathologist)], true, true),
        Body = new BodyDefinition(
            [
                new GroupDefinition(Guid.NewGuid(), "HEMATOLOGY", true,
                [
                    new RowDefinition(HemoglobinId, RowKind.Test, "Hemoglobin"),
                    new RowDefinition(HeadingId, RowKind.Section, "DIFFERENTIAL"),
                ]),
                new GroupDefinition(Guid.NewGuid(), null, false, [new RowDefinition(GlucoseId, RowKind.Test, "Glucose")]),
            ],
            [new TextboxDefinition(FindingsId, "Findings")]),
    };

    private static LabResultHeader Header(string name = "Ann Lee") => new(
        null, null, "P-1", name, "30", "F", "ACME", new DateOnly(2026, 3, 1), "ok", "Tech One", "Dr Path",
        MedicalTechnologistLicense: "L1", PathologistLicense: "L3", MedicalTechnologist2: "Tech Two", MedicalTechnologist2License: "L2");

    private static Dictionary<string, string?> Values(string? hemoglobin = "13.5", string? reference = "12-16", string? glucose = "90", string? findings = "Normal") => new()
    {
        [CustomFormValues.ResultKey(HemoglobinId)] = hemoglobin,
        [CustomFormValues.ReferenceKey(HemoglobinId)] = reference,
        [CustomFormValues.ResultKey(GlucoseId)] = glucose,
        [CustomFormValues.TextKey(FindingsId)] = findings,
    };

    private void SignIn(params int[] modules) =>
        _env.Session.SignIn(new AuthenticatedUser(3, "u", "U", false, false, [.. modules.Select(m => new ModulePermission(m, true, true, true, true))]));

    /// <summary>Two forms (modules 1000 and 1001), each with version 1 of <see cref="Design"/>.</summary>
    private async Task<AppDbContext> StartAsync()
    {
        var db = _env.CreateDb();
        foreach (var (moduleId, name) in new[] { (FormA, "Lipid Profile"), (FormB, "Thyroid Panel") })
        {
            db.Modules.Add(new Module { Id = moduleId, ModuleName = name, IsActive = true });
            var form = new CustomForm { Name = name, ModuleId = moduleId };
            form.Versions.Add(new CustomFormVersion { VersionNumber = 1, DefinitionJson = Design().ToJson() });
            db.CustomForms.Add(form);
        }

        await db.SaveChangesAsync();
        SignIn(FormA, FormB);
        return db;
    }

    private CustomFormResultService For(AppDbContext db, int moduleId = FormA) =>
        new(db, _env.Session, _env.Clock, new CustomFormContext { ModuleId = moduleId });

    private static CustomFormInput Input(LabResultHeader? header = null, IReadOnlyDictionary<string, string?>? values = null, long id = 0, byte[]? rowVersion = null) =>
        new(id, header ?? Header(), values ?? Values(), rowVersion);

    private static async Task AddVersion2Async(AppDbContext db, int moduleId, FormDefinition design)
    {
        var form = await db.CustomForms.SingleAsync(f => f.ModuleId == moduleId);
        db.CustomFormVersions.Add(new CustomFormVersion { CustomFormId = form.Id, VersionNumber = 2, DefinitionJson = design.ToJson() });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task The_service_needs_the_module_of_a_form()
    {
        await using var db = await StartAsync();

        Assert.Throws<InvalidOperationException>(() => new CustomFormResultService(db, _env.Session, _env.Clock, new CustomFormContext()));
        Assert.Throws<InvalidOperationException>(() => new CustomFormResultService(db, _env.Session, _env.Clock, new CustomFormContext { ModuleId = ModuleIds.Urinalysis }));
    }

    [Fact]
    public async Task A_result_is_saved_with_its_header_and_values_and_read_back_with_its_design()
    {
        await using var db = await StartAsync();
        var service = For(db);

        var saved = await service.SaveAsync(Input());

        Assert.True(saved.IsSuccess, saved.IsFailure ? saved.Error.Message : "");
        var details = (await service.GetAsync(saved.Value.Id)).Value;
        Assert.Equal("Ann Lee", details.Header.PatientName);
        Assert.Equal("Tech Two", details.Header.MedicalTechnologist2);
        Assert.Equal("L3", details.Header.PathologistLicense);
        Assert.Equal(1, details.Design.VersionNumber);
        Assert.Equal("Lipid Profile", details.Design.Name);
        Assert.Equal("13.5", details.Values[CustomFormValues.ResultKey(HemoglobinId)]);
        Assert.Equal("12-16", details.Values[CustomFormValues.ReferenceKey(HemoglobinId)]);
        Assert.Equal("90", details.Values[CustomFormValues.ResultKey(GlucoseId)]);
        Assert.Equal("Normal", details.Values[CustomFormValues.TextKey(FindingsId)]);

        var report = await db.LabReports.SingleAsync();
        Assert.Equal(LabReportType.CustomForm, report.ReportType);
        Assert.Equal(1, (await db.CustomFormReports.Include(c => c.CustomFormVersion).SingleAsync()).CustomFormVersion.VersionNumber);
    }

    [Fact]
    public async Task Only_values_the_design_has_a_place_for_are_kept_and_empty_ones_are_dropped()
    {
        await using var db = await StartAsync();
        var values = Values(hemoglobin: "  14  ", reference: " ", glucose: null, findings: "Fine");
        values["not-a-row"] = "stray";
        values[CustomFormValues.ResultKey(HeadingId)] = "headings have no result";

        var saved = (await For(db).SaveAsync(Input(values: values))).Value;

        Assert.Equal(
            [CustomFormValues.ResultKey(HemoglobinId), CustomFormValues.TextKey(FindingsId)],
            saved.Values.Keys);
        Assert.Equal("14", saved.Values[CustomFormValues.ResultKey(HemoglobinId)]);
        var stored = JsonSerializer.Deserialize<Dictionary<string, string>>((await db.CustomFormReports.SingleAsync()).ValuesJson)!;
        Assert.Equal(2, stored.Count);
    }

    [Fact]
    public async Task An_old_result_keeps_the_version_it_was_saved_with_and_new_ones_use_the_newest()
    {
        await using var db = await StartAsync();
        var first = (await For(db).SaveAsync(Input())).Value;

        // version 2 drops Glucose and renames Hemoglobin
        var v2 = Design() with
        {
            Body = new BodyDefinition(
                [new GroupDefinition(Guid.NewGuid(), null, true, [new RowDefinition(HemoglobinId, RowKind.Test, "Hgb")])], []),
        };
        await AddVersion2Async(db, FormA, v2);

        var service = For(db);
        var old = (await service.GetAsync(first.Id)).Value;
        Assert.Equal(1, old.Design.VersionNumber);
        Assert.Equal("Hemoglobin", old.Design.Definition.Body.Groups[0].Rows[0].Name);
        Assert.Equal("90", old.Values[CustomFormValues.ResultKey(GlucoseId)]);

        var second = (await service.SaveAsync(Input())).Value;
        Assert.Equal(2, second.Design.VersionNumber);
        Assert.False(second.Values.ContainsKey(CustomFormValues.ResultKey(GlucoseId))); // Glucose is no longer part of the form

        // editing the old result keeps it on version 1 (its glucose value is still kept)
        var edited = (await service.SaveAsync(Input(values: Values(hemoglobin: "15"), id: first.Id, rowVersion: old.RowVersion))).Value;
        Assert.Equal(1, edited.Design.VersionNumber);
        Assert.Equal("15", edited.Values[CustomFormValues.ResultKey(HemoglobinId)]);
        Assert.Equal("90", edited.Values[CustomFormValues.ResultKey(GlucoseId)]);

        var design = (await service.GetDesignAsync()).Value;
        Assert.Equal(2, design.VersionNumber);
    }

    [Fact]
    public async Task Values_longer_than_their_field_allows_are_refused_with_the_name_of_the_field()
    {
        await using var db = await StartAsync();
        var service = For(db);

        var result = await service.SaveAsync(Input(values: Values(hemoglobin: new string('x', CustomFormValues.ResultMaxLength + 1))));
        Assert.Contains("Hemoglobin result", result.Error.Message);

        Assert.Contains("Hemoglobin reference value", (await service.SaveAsync(Input(values: Values(reference: new string('x', CustomFormValues.ReferenceMaxLength + 1))))).Error.Message);
        Assert.Contains("Findings", (await service.SaveAsync(Input(values: Values(findings: new string('x', CustomFormValues.TextMaxLength + 1))))).Error.Message);
        Assert.True((await service.SaveAsync(Input(values: Values(hemoglobin: new string('x', CustomFormValues.ResultMaxLength))))).IsSuccess);
        Assert.Equal(1, await db.LabReports.CountAsync());
    }

    [Fact]
    public async Task The_patient_name_is_still_required()
    {
        await using var db = await StartAsync();

        var result = await For(db).SaveAsync(Input(Header(" ")));

        Assert.True(result.IsFailure);
        Assert.Contains("Patient name", result.Error.Message);
    }

    [Fact]
    public async Task Each_form_only_sees_its_own_results()
    {
        await using var db = await StartAsync();
        var a = (await For(db, FormA).SaveAsync(Input(Header("Alpha Patient")))).Value;
        var b = (await For(db, FormB).SaveAsync(Input(Header("Bravo Patient")))).Value;

        var listA = (await For(db, FormA).SearchAsync(new LabResultSearch(null))).Value;
        var listB = (await For(db, FormB).SearchAsync(new LabResultSearch(null))).Value;
        Assert.Equal(["Alpha Patient"], listA.Items.Select(i => i.PatientName));
        Assert.Equal(["Bravo Patient"], listB.Items.Select(i => i.PatientName));

        Assert.Equal("Result.NotFound", (await For(db, FormA).GetAsync(b.Id)).Error.Code);
        Assert.Equal("Result.NotFound", (await For(db, FormA).SaveAsync(Input(id: b.Id))).Error.Code);
        Assert.Equal("Result.NotFound", (await For(db, FormA).DeleteAsync(b.Id)).Error.Code);
        Assert.Equal("Result.NotFound", (await For(db, FormA).GetPrintableAsync(b.Id)).Error.Code);
        Assert.Equal(2, await db.LabReports.CountAsync());

        Assert.True((await For(db, FormB).DeleteAsync(b.Id)).IsSuccess);
        Assert.Equal(a.Id, (await db.LabReports.SingleAsync()).Id);
    }

    [Fact]
    public async Task A_form_needs_its_own_permission_for_each_action()
    {
        await using var db = await StartAsync();
        var saved = (await For(db).SaveAsync(Input())).Value;

        SignIn(FormB); // access to another form is not access to this one
        Assert.Equal("Auth.Forbidden", (await For(db).GetAsync(saved.Id)).Error.Code);
        Assert.Equal("Auth.Forbidden", (await For(db).SaveAsync(Input())).Error.Code);
        Assert.Equal("Auth.Forbidden", (await For(db).GetDesignAsync()).Error.Code);
        Assert.Equal("Auth.Forbidden", (await For(db).GetPrintableAsync(saved.Id)).Error.Code);

        _env.Session.SignIn(new AuthenticatedUser(3, "u", "U", false, false, [new ModulePermission(FormA, false, false, false, false)]));
        Assert.True((await For(db).GetAsync(saved.Id)).IsSuccess);
        Assert.True((await For(db).GetDesignAsync()).IsSuccess);
        Assert.Equal("Auth.Forbidden", (await For(db).SaveAsync(Input())).Error.Code);
        Assert.Equal("Auth.Forbidden", (await For(db).SaveAsync(Input(id: saved.Id))).Error.Code);
        Assert.Equal("Auth.Forbidden", (await For(db).DeleteAsync(saved.Id)).Error.Code);
        Assert.Equal("Auth.Forbidden", (await For(db).GetPrintableAsync(saved.Id)).Error.Code);
    }

    [Fact]
    public async Task A_switched_off_form_takes_no_new_results_but_old_ones_can_be_edited_and_printed()
    {
        await using var db = await StartAsync();
        var saved = (await For(db).SaveAsync(Input())).Value;
        var form = await db.CustomForms.SingleAsync(f => f.ModuleId == FormA);
        form.IsActive = false;
        await db.SaveChangesAsync();

        var refused = await For(db).SaveAsync(Input());
        Assert.Contains("switched off", refused.Error.Message);

        Assert.True((await For(db).SaveAsync(Input(values: Values(hemoglobin: "16"), id: saved.Id, rowVersion: saved.RowVersion))).IsSuccess);
        Assert.True((await For(db).GetPrintableAsync(saved.Id)).IsSuccess);
        Assert.Equal(1, await db.LabReports.CountAsync());
    }

    [Fact]
    public async Task A_second_result_for_a_registration_asks_first_and_names_the_form()
    {
        await using var db = await StartAsync();
        var registration = new Domain.Registrations.PatientRegistration
        {
            RegistrationCode = "R-1",
            Patient = new Domain.Patients.Patient { PatientCode = "P-1", PatientName = "Ann Lee" },
            InputDate = DateTime.UtcNow,
        };
        db.PatientRegistrations.Add(registration);
        await db.SaveChangesAsync();
        var header = Header() with { RegistrationId = registration.Id };

        Assert.True((await For(db).SaveAsync(Input(header))).IsSuccess);
        var second = await For(db).SaveAsync(Input(header));
        Assert.Equal(LabResultService<CustomFormInput, CustomFormDetails, CustomFormReport>.DuplicateCode, second.Error.Code);
        Assert.Contains("Lipid Profile", second.Error.Message);
        Assert.True((await For(db).SaveAsync(Input(header with { ConfirmedDuplicate = true }))).IsSuccess);

        // a result of the other form does not count as a repeat
        Assert.True((await For(db, FormB).SaveAsync(Input(header))).IsSuccess);
    }

    [Fact]
    public async Task The_printout_data_has_the_forms_name_values_and_the_signatories_of_its_design()
    {
        await using var db = await StartAsync();
        var saved = (await For(db).SaveAsync(Input())).Value;

        var printable = (await For(db).GetPrintableAsync(saved.Id)).Value;

        Assert.Equal(ReportLayout.CustomForm, printable.Layout);
        Assert.Equal("Lipid Profile", printable.Title);
        Assert.Equal("13.5", printable.Fields![CustomFormValues.ResultKey(HemoglobinId)]);
        Assert.Equal(
            [("Medical Technologist", "Tech One", "L1"), ("Medical Technologist", "Tech Two", "L2"), ("Pathologist", "Dr Path", "L3")],
            printable.Signatories.Select(s => (s.Role, s.Name, s.LicenseNo)));
        Assert.Equal(LabResultService<CustomFormInput, CustomFormDetails, CustomFormReport>.ElectronicNote, printable.FooterNote);
    }

    [Fact]
    public async Task Licence_numbers_are_left_off_when_the_form_says_so_and_only_the_designed_signatories_print()
    {
        await using var db = await StartAsync();
        var saved = (await For(db).SaveAsync(Input())).Value;
        var v2 = Design() with { Footer = new FooterDefinition([new(SignatoryRole.Pathologist)], false, true) };
        await AddVersion2Async(db, FormA, v2);
        var newer = (await For(db).SaveAsync(Input())).Value;

        var printable = (await For(db).GetPrintableAsync(newer.Id)).Value;
        Assert.Equal([("Pathologist", "Dr Path", (string?)null)], printable.Signatories.Select(s => (s.Role, s.Name, s.LicenseNo)));

        // the older result still prints with the signatories of version 1
        Assert.Equal(3, (await For(db).GetPrintableAsync(saved.Id)).Value.Signatories.Count);
    }

    [Fact]
    public async Task The_page_is_built_from_the_design_header_cells_groups_rows_text_boxes_and_remarks()
    {
        await using var db = await StartAsync();
        var saved = (await For(db).SaveAsync(Input(values: Values(findings: "Seen by the pathologist")))).Value;

        // printing through the shared interface (as Batch Print and the screens do) reaches the custom form's own printout
        ILabResultPrinting printing = For(db);
        var printable = (await printing.GetPrintableAsync(saved.Id)).Value;

        var custom = printable.Custom!;
        Assert.Equal(6, custom.HeaderCells.Count);
        var date = new DateOnly(2026, 3, 1).ToString("d", System.Globalization.CultureInfo.CurrentCulture);
        Assert.Equal(
            [("Patient Code", "P-1"), ("Age", "30"), ("Sex/Gender", "F"), ("Patient Name", "Ann Lee"), ("Date Requested", date), ("Company", "ACME")],
            custom.HeaderCells.Take(6).Select(c => (c!.Label, c.Value)));

        Assert.Equal(["HEMATOLOGY", null], custom.Groups.Select(g => g.Title));
        Assert.Equal([true, false], custom.Groups.Select(g => g.ShowReference));
        Assert.Equal(
            [(false, "Hemoglobin", "13.5", "12-16"), (true, "DIFFERENTIAL", null, null)],
            custom.Groups[0].Rows.Select(r => (r.IsSection, r.Name, r.Result, r.Reference)));
        Assert.Equal(("Glucose", "90"), (custom.Groups[1].Rows[0].Name, custom.Groups[1].Rows[0].Result));
        Assert.Equal([("Findings", "Seen by the pathologist")], custom.Textboxes.Select(t => (t.Label, t.Text)));
        Assert.True(custom.ShowRemarks);
        Assert.True(custom.ShowLicenseNumber);
        Assert.Equal("ok", printable.ResultTexts.Single(t => t.Label == "Remarks").Text);
    }

    [Fact]
    public async Task A_design_without_remarks_leaves_the_remarks_text_off_the_page_and_empty_header_cells_stay_empty()
    {
        await using var db = await StartAsync();
        var form = await db.CustomForms.Include(f => f.Versions).SingleAsync(f => f.ModuleId == FormA);
        var noRemarks = Design() with
        {
            Header = [HeaderField.PatientName, null, HeaderField.Age, null, null, HeaderField.Company],
            Footer = new FooterDefinition([], false, false),
        };
        form.Versions[0].DefinitionJson = noRemarks.ToJson();
        await db.SaveChangesAsync();
        var saved = (await For(db).SaveAsync(Input())).Value;

        var printable = (await For(db).GetPrintableAsync(saved.Id)).Value;

        Assert.False(printable.Custom!.ShowRemarks);
        Assert.DoesNotContain(printable.ResultTexts, t => t.Label == "Remarks");
        Assert.Empty(printable.Signatories);
        Assert.Equal(
            ["Patient Name", null, "Age", null, null, "Company"],
            printable.Custom.HeaderCells.Select(c => c?.Label));
    }

    [Fact]
    public async Task A_result_of_an_older_version_prints_with_its_own_design()
    {
        await using var db = await StartAsync();
        var saved = (await For(db).SaveAsync(Input())).Value;
        await AddVersion2Async(db, FormA, Design() with { Body = new BodyDefinition([new GroupDefinition(Guid.NewGuid(), "ONLY", true, [])], []) });

        var printable = (await For(db).GetPrintableAsync(saved.Id)).Value;

        Assert.Equal(["HEMATOLOGY", null], printable.Custom!.Groups.Select(g => g.Title));
        Assert.Single(printable.Custom.Textboxes);
    }

    [Fact]
    public async Task A_result_can_be_made_for_a_patient_who_is_not_registered_and_shows_in_the_list()
    {
        await using var db = await StartAsync();
        var service = For(db);
        await service.SaveAsync(Input(Header("Walk In")));

        var list = (await service.SearchAsync(new LabResultSearch("Walk"))).Value;

        var item = Assert.Single(list.Items);
        Assert.Equal("Walk In", item.PatientName);
        Assert.Null(item.RegistrationCode);
    }
}
