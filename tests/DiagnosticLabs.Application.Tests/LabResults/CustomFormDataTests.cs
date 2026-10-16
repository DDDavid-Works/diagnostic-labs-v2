using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Entries;
using DiagnosticLabs.Domain.Identity;
using DiagnosticLabs.Domain.Lab;
using DiagnosticLabs.Domain.Lab.Reports;
using DiagnosticLabs.Domain.Settings;
using DiagnosticLabs.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace DiagnosticLabs.Application.Tests.LabResults;

/// <summary>The tables behind the Custom Lab Form designer: forms, versions and results, and how they fit the existing module, header and entry-list code.</summary>
public class CustomFormDataTests
{
    private const int CustomModuleId = ModuleIds.FirstCustomForm;

    private readonly TestEnvironment _env = new();

    private static IModel BuildModel()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer("Server=none;Database=none").Options;
        using var db = new AppDbContext(options);
        return db.GetService<IDesignTimeModel>().Model;
    }

    [Fact]
    public void Custom_form_modules_start_above_every_built_in_module_id()
    {
        var ids = typeof(ModuleIds).GetFields().Where(f => f.IsLiteral && f.FieldType == typeof(int)).ToDictionary(f => f.Name, f => (int)f.GetRawConstantValue()!);

        Assert.Equal(ids.Count, ids.Values.Distinct().Count());
        Assert.All(ids.Where(i => i.Key != nameof(ModuleIds.FirstCustomForm)), i => Assert.True(i.Value < ModuleIds.FirstCustomForm, i.Key));
    }

    [Fact]
    public void Versions_and_modules_are_unique_and_results_follow_their_header()
    {
        var model = BuildModel();

        Assert.Contains(model.FindEntityType(typeof(CustomForm))!.GetIndexes(), i => i.IsUnique && i.Properties.Single().Name == nameof(CustomForm.ModuleId));
        Assert.Contains(
            model.FindEntityType(typeof(CustomFormVersion))!.GetIndexes(),
            i => i.IsUnique && i.Properties.Select(p => p.Name).SequenceEqual([nameof(CustomFormVersion.CustomFormId), nameof(CustomFormVersion.VersionNumber)]));

        var report = model.FindEntityType(typeof(CustomFormReport))!;
        Assert.Equal("CustomFormReports", report.GetTableName());
        Assert.Equal(DeleteBehavior.Cascade, report.GetForeignKeys().Single(f => f.PrincipalEntityType.ClrType == typeof(LabReport)).DeleteBehavior);
        Assert.Equal(DeleteBehavior.Restrict, report.GetForeignKeys().Single(f => f.PrincipalEntityType.ClrType == typeof(CustomFormVersion)).DeleteBehavior);
        Assert.NotEmpty(report.GetDeclaredQueryFilters());
    }

    [Fact]
    public void An_entry_list_name_made_from_a_row_id_fits_the_column()
    {
        var maxLength = BuildModel().FindEntityType(typeof(LookupValue))!.FindProperty(nameof(LookupValue.FieldName))!.GetMaxLength();

        Assert.True(Guid.NewGuid().ToString("N").Length <= maxLength);
    }

    [Fact]
    public async Task A_result_is_a_lab_report_plus_the_values_of_the_version_it_was_saved_with()
    {
        await using var db = _env.CreateDb();
        var form = NewForm("Lipid Profile");
        form.Versions.Add(new CustomFormVersion { VersionNumber = 1, DefinitionJson = """{"v":1}""" });
        form.Versions.Add(new CustomFormVersion { VersionNumber = 2, DefinitionJson = """{"v":2}""" });
        db.CustomForms.Add(form);
        db.LabReports.Add(new LabReport { ReportType = LabReportType.CustomForm, PatientCode = "P-1", PatientName = "Ann", DateRequested = new DateTime(2026, 3, 1) });
        await db.SaveChangesAsync();

        var first = form.Versions.Single(v => v.VersionNumber == 1);
        var reportId = db.LabReports.Single().Id;
        db.Set<CustomFormReport>().Add(new CustomFormReport { LabReportId = reportId, CustomFormVersionId = first.Id, ValuesJson = """{"6f1c":"5.2"}""" });
        await db.SaveChangesAsync();

        // a later design does not change what the old result points to
        var saved = await db.Set<CustomFormReport>().Include(r => r.CustomFormVersion).ThenInclude(v => v.CustomForm).SingleAsync();
        Assert.Equal("""{"v":1}""", saved.CustomFormVersion.DefinitionJson);
        Assert.Equal("""{"6f1c":"5.2"}""", saved.ValuesJson);
        Assert.Equal("Lipid Profile", saved.CustomFormVersion.CustomForm.Name);
        Assert.Equal(2, await db.CustomFormVersions.MaxAsync(v => v.VersionNumber));
    }

    [Fact]
    public async Task A_deleted_header_hides_the_custom_result_too()
    {
        await using var db = _env.CreateDb();
        var form = NewForm("Any");
        form.Versions.Add(new CustomFormVersion { VersionNumber = 1, DefinitionJson = "{}" });
        db.CustomForms.Add(form);
        var header = new LabReport { ReportType = LabReportType.CustomForm, PatientCode = "P-1", PatientName = "Ann", DateRequested = new DateTime(2026, 3, 1) };
        db.LabReports.Add(header);
        await db.SaveChangesAsync();
        db.Set<CustomFormReport>().Add(new CustomFormReport { LabReportId = header.Id, CustomFormVersionId = form.Versions[0].Id });
        await db.SaveChangesAsync();

        header.IsDeleted = true;
        await db.SaveChangesAsync();

        Assert.Empty(await db.Set<CustomFormReport>().ToListAsync());
    }

    [Fact]
    public async Task A_custom_forms_dropdown_uses_the_entry_service_under_the_forms_own_module_permission()
    {
        await using var db = _env.CreateDb();
        db.Modules.Add(new Module { Id = CustomModuleId, ModuleName = "Lipid Profile", IsActive = true });
        await db.SaveChangesAsync();
        var rowId = Guid.NewGuid().ToString("N");
        var field = new EntryField(rowId, EntryKind.SingleLine, CustomModuleId);
        var service = new EntryService(db, _env.Session);

        _env.Session.SignIn(new AuthenticatedUser(3, "u", "U", false, false, [new ModulePermission(CustomModuleId, true, true, true, false)]));
        var saved = await service.SaveSingleLineAsync(field, CustomModuleId, [new SingleLineEntry(0, "Positive"), new SingleLineEntry(0, "Negative")]);

        Assert.True(saved.IsSuccess);
        Assert.Equal(["Positive", "Negative"], await service.GetChoicesAsync(field));
        Assert.Equal("Lipid Profile", saved.Value.ScopeName);
        Assert.Equal(rowId, saved.Value.FieldName); // without a title the list is called by its name

        // a row's list is shown under the row's name (the id is only what it is stored under), and it is the same list either way
        var titled = field with { Title = "Hemoglobin" };
        Assert.Equal("Hemoglobin", (await service.GetSingleLineAsync(titled, CustomModuleId)).Value.FieldName);
        Assert.Equal(["Positive", "Negative"], await service.GetChoicesAsync(titled));

        var box = new EntryField(Guid.NewGuid().ToString("N"), EntryKind.MultiLine, CustomModuleId, Title: "Findings");
        Assert.Equal("Findings", (await service.GetMultiLineAsync(box, CustomModuleId)).Value.FieldName);

        // another form's list with the same name stays separate, and a user without access to the form is refused
        Assert.Empty(await service.GetChoicesAsync(field with { ModuleId = CustomModuleId + 1 }));
        _env.Session.SignIn(new AuthenticatedUser(4, "v", "V", false, false, []));
        Assert.Equal("Auth.Forbidden", (await service.SaveSingleLineAsync(field, CustomModuleId, [new SingleLineEntry(0, "X")])).Error.Code);
    }

    private static CustomForm NewForm(string name) => new() { Name = name, ModuleId = CustomModuleId };
}
