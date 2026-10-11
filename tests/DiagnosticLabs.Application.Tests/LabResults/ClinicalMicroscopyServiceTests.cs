using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Board;
using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Application.LabResults;
using DiagnosticLabs.Domain.Lab;
using DiagnosticLabs.Domain.Lab.Reports;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Application.Tests.LabResults;

public class ClinicalMicroscopyServiceTests
{
    private readonly TestEnvironment _env = new();

    public ClinicalMicroscopyServiceTests() => _env.Clock.UtcNow = new DateTime(2026, 10, 9, 4, 0, 0, DateTimeKind.Utc);

    private DateOnly Today => LocalTime.ToLocalDate(_env.Clock.UtcNow);

    private ClinicalMicroscopyService CreateService(Infrastructure.Persistence.AppDbContext db) => new(db, _env.Session, _env.Clock);

    private void SignInAsAdmin() => _env.Session.SignIn(new AuthenticatedUser(1, "admin", "Admin", true, false, []));

    private LabResultHeader Header() =>
        new(null, null, null, "Rosa Dimaano", "41", "Female", "ACME", Today, "Repeat if needed", "DR. A", "DR. B", MedicalTechnologist2: "DR. C");

    private static IEnumerable<string> AllColumns() => ClinicalMicroscopyFields.Tests.SelectMany(t => new[] { t.ResultColumn, t.ReferenceColumn });

    // Every column gets its own value, so a value that lands in the wrong column shows.
    private static ClinicalMicroscopyData FullData() => new(AllColumns().ToDictionary(c => c, c => (string?)("v-" + c)));

    private ClinicalMicroscopyInput Input(ClinicalMicroscopyData? data = null) => new(0, Header(), data ?? FullData(), null);

    [Fact]
    public void Every_test_of_the_form_is_a_column_pair_of_the_report()
    {
        var columns = typeof(ClinicalMicroscopyReport).GetProperties().Select(p => p.Name).ToHashSet();

        Assert.All(AllColumns(), name => Assert.Contains(name, columns));
        Assert.Equal(AllColumns().Count(), columns.Count - 2 /* LabReportId, LabReport */);
    }

    [Fact]
    public void The_form_has_the_three_parts_and_twenty_tests_of_the_printed_form_in_order()
    {
        Assert.Equal(["PHYSICAL EXAMINATION", "CHEMICAL EXAMINATION", "MICROSCOPIC EXAMINATION"], ClinicalMicroscopyFields.Sections.Select(s => s.Title));
        Assert.Equal(
            ["Color", "Transparency", "pH", "Specific Gravity", "Protein", "Glucose", "Blood", "Leukocytes", "Nitrite", "Bilirubin", "Urobilinogen", "Ketones",
             "WBC", "RBC", "Mucus Threads", "Epithelial Cells", "Amorphous Urates/Phosphates", "Bacteria", "Crystals", "Casts"],
            ClinicalMicroscopyFields.Tests.Select(t => t.Label));

        // the numbers are typed; every other result is picked from a list of its own
        Assert.Equal(["pH", "Specific Gravity", "WBC", "RBC"], ClinicalMicroscopyFields.Tests.Where(t => !t.IsChoice).Select(t => t.Label));
        Assert.All(ClinicalMicroscopyFields.Tests.Where(t => t.IsChoice), t => Assert.Equal(ModuleIds.ClinicalMicroscopy, t.Entry.ModuleId));
    }

    [Fact]
    public void The_module_is_wired_in_like_the_other_result_forms()
    {
        Assert.Equal(LabReportType.ClinicalMicroscopy, LabResultModules.ReportTypeOf(ModuleIds.ClinicalMicroscopy));
    }

    [Fact]
    public async Task Every_value_is_saved_and_read_back_in_its_own_place()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var service = CreateService(db);

        var saved = (await service.SaveAsync(Input())).Value;
        var reopened = (await service.GetAsync(saved.Id)).Value;

        Assert.All(AllColumns(), c => Assert.Equal("v-" + c, reopened.Data.Value(c)));
        Assert.Equal(LabReportType.ClinicalMicroscopy, (await db.LabReports.AsNoTracking().SingleAsync()).ReportType);
        Assert.Equal(("DR. C", "Repeat if needed"), (reopened.Header.MedicalTechnologist2, reopened.Header.Remarks));
    }

    [Fact]
    public async Task A_form_with_nothing_typed_is_empty_and_blank_values_are_kept_empty()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var service = CreateService(db);

        var empty = (await service.SaveAsync(Input(ClinicalMicroscopyData.Empty))).Value;
        var blank = (await service.SaveAsync(Input(new ClinicalMicroscopyData(new Dictionary<string, string?> { ["ColorResult"] = "  ", ["CastsReference"] = " lpf " })) with { Header = Header() with { PatientName = "Second" } })).Value;

        Assert.All(AllColumns(), c => Assert.Null(empty.Data.Value(c)));
        Assert.Null(blank.Data.Value("ColorResult"));
        Assert.Equal("lpf", blank.Data.Value("CastsReference"));
    }

    [Theory]
    [InlineData("ColorResult", ClinicalMicroscopyFields.ResultMaxLength)]
    [InlineData("WBCResult", ClinicalMicroscopyFields.ResultMaxLength)]
    [InlineData("ProteinReference", ClinicalMicroscopyFields.ReferenceMaxLength)]
    public async Task A_value_that_is_too_long_is_refused(string column, int max)
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var service = CreateService(db);

        var result = await service.SaveAsync(Input(new ClinicalMicroscopyData(new Dictionary<string, string?> { [column] = new string('x', max + 1) })));

        Assert.True(result.IsFailure);
        Assert.Empty(await db.LabReports.ToListAsync());
    }

    [Fact]
    public async Task The_printout_carries_every_value_by_name_and_has_three_signatories_with_licences_and_remarks()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var service = CreateService(db);
        var header = Header() with { MedicalTechnologistLicense = "111", MedicalTechnologist2License = "222", PathologistLicense = "333" };
        var saved = (await service.SaveAsync(new ClinicalMicroscopyInput(0, header, FullData(), null))).Value;

        var print = (await service.GetPrintableAsync(saved.Id)).Value;

        Assert.Equal(ReportLayout.ClinicalMicroscopy, print.Layout);
        Assert.Equal("Clinical Microscopy", print.Title);
        Assert.Equal("v-PHReference", print.Fields!["PHReference"]);
        Assert.Equal("v-AmorphousUratesPhosphatesResult", print.Fields["AmorphousUratesPhosphatesResult"]);
        Assert.Equal(AllColumns().Count(), print.Fields.Count);
        Assert.Equal(["DR. A", "DR. C", "DR. B"], print.Signatories.Select(s => s.Name));
        Assert.Equal(["111", "222", "333"], print.Signatories.Select(s => s.LicenseNo));
        Assert.Equal("Repeat if needed", print.ResultTexts.Single(t => t.Label == "Remarks").Text);
        Assert.Equal(["* This is a validated and original report *", "This is an electronically signed document"], print.FooterNote.Split('\n'));
    }

    [Fact]
    public async Task Printing_needs_the_print_permission_and_an_existing_result()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var service = CreateService(db);
        var saved = (await service.SaveAsync(Input())).Value;

        Assert.Equal("Clinical Microscopy.NotFound", (await service.GetPrintableAsync(999)).Error.Code);

        _env.Session.SignIn(new AuthenticatedUser(7, "viewer", "Viewer", false, false, [new ModulePermission(ModuleIds.ClinicalMicroscopy, false, false, false, false)]));
        Assert.Equal("Auth.Forbidden", (await service.GetPrintableAsync(saved.Id)).Error.Code);
    }
}
