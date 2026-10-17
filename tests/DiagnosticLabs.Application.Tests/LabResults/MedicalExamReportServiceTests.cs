using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Board;
using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Application.LabResults;
using DiagnosticLabs.Domain.Lab;
using DiagnosticLabs.Domain.Lab.Reports;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Application.Tests.LabResults;

public class MedicalExamReportServiceTests
{
    private readonly TestEnvironment _env = new();

    public MedicalExamReportServiceTests() => _env.Clock.UtcNow = new DateTime(2026, 10, 16, 4, 0, 0, DateTimeKind.Utc);

    private DateOnly Today => LocalTime.ToLocalDate(_env.Clock.UtcNow);

    private MedicalExamReportService CreateService(Infrastructure.Persistence.AppDbContext db) => new(db, _env.Session, _env.Clock);

    private void SignInAsAdmin() => _env.Session.SignIn(new AuthenticatedUser(1, "admin", "Admin", true, false, []));

    private LabResultHeader Header() =>
        new(null, null, null, "Rosa Dimaano", "41", "Female", "ACME", Today, "Fit to work.", "DR. A", "DR. MALONZO", PathologistLicense: "87844");

    // Every field gets its own value (a tick takes the next of its options), so a value that lands in the wrong column shows.
    private static string? ValueFor(MedicalExamField field, int index) =>
        field.Options is { } options ? options[index % options.Count] : "v-" + field.Name;

    private static MedicalExamData FullData() =>
        new(MedicalExamFields.All.Select((f, i) => (f.Name, Value: ValueFor(f, i))).ToDictionary(x => x.Name, x => (string?)x.Value));

    private MedicalExamReportInput Input(MedicalExamData? data = null) => new(0, Header(), data ?? FullData(), null);

    [Fact]
    public void Every_field_of_the_form_is_a_column_of_the_report_with_the_same_length()
    {
        // The service reads and writes the fields by name, so a name without a column would fail the moment the service is first used.
        using var db = _env.CreateDb();
        var entity = db.Model.FindEntityType(typeof(MedicalExamReport))!;
        var columns = typeof(MedicalExamReport).GetProperties().Select(p => p.Name).ToHashSet();

        Assert.All(MedicalExamFields.All, f =>
        {
            Assert.Contains(f.Name, columns);
            Assert.Equal(f.MaxLength, entity.FindProperty(f.Name)!.GetMaxLength());
        });
        Assert.Equal(MedicalExamFields.All.Count, columns.Count - 2 /* LabReportId, LabReport */);
        Assert.Equal(MedicalExamFields.All.Count, MedicalExamFields.All.Select(f => f.Name).Distinct().Count());
    }

    [Fact]
    public void The_history_has_the_44_rows_of_the_printed_form_and_the_review_of_systems_15()
    {
        Assert.Equal([13, 13, 13, 5], MedicalExamFields.HistoryColumns.Select(c => c.Length));
        Assert.Equal(44, MedicalExamFields.HistoryColumns.Sum(c => c.Length));
        Assert.Equal(15, MedicalExamFields.ReviewColumns.Sum(c => c.Length));
        Assert.Equal(["A", "B", "C", "D", "Pending"], MedicalExamFields.Get("Classification").Options);
    }

    [Fact]
    public void The_module_is_wired_in_like_the_other_result_forms()
    {
        Assert.Equal(LabReportType.MedicalExamReport, LabResultModules.ReportTypeOf(ModuleIds.MedicalExamReport));
    }

    [Fact]
    public async Task Every_field_is_saved_and_read_back_in_its_own_place()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var service = CreateService(db);
        var expected = FullData();

        var saved = (await service.SaveAsync(Input())).Value;
        var reopened = (await service.GetAsync(saved.Id)).Value;

        foreach (var field in MedicalExamFields.All)
            Assert.Equal(expected.Value(field.Name), reopened.Data.Value(field.Name));

        Assert.Equal(LabReportType.MedicalExamReport, (await db.LabReports.AsNoTracking().SingleAsync()).ReportType);
        Assert.Equal("DR. MALONZO", reopened.Header.Pathologist);
        Assert.Equal("87844", reopened.Header.PathologistLicense);
    }

    [Fact]
    public async Task An_empty_form_can_be_saved_and_every_value_stays_empty()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var service = CreateService(db);

        var saved = (await service.SaveAsync(Input(MedicalExamData.Empty))).Value;

        Assert.All(MedicalExamFields.All, f => Assert.Null(saved.Data.Value(f.Name)));
    }

    [Fact]
    public async Task Blank_values_are_kept_empty_and_typed_text_is_trimmed()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var service = CreateService(db);
        var data = new MedicalExamData(new Dictionary<string, string?> { ["HxAsthma"] = "   ", ["Findings"] = "  none seen  " });

        var saved = (await service.SaveAsync(Input(data))).Value;

        Assert.Null(saved.Data.Value("HxAsthma"));
        Assert.Equal("none seen", saved.Data.Value("Findings"));
    }

    [Theory]
    [InlineData("Ishihara", 100)]
    [InlineData("Bp1", MedicalExamFields.ShortMaxLength)]
    [InlineData("BmiCategory", MedicalExamFields.TextMaxLength)]
    [InlineData("Findings", MedicalExamFields.LongTextMaxLength)]
    [InlineData("Assessment", MedicalExamFields.LongTextMaxLength)]
    [InlineData("LabCbcNote", 200)]
    [InlineData("NotedBy", 100)]
    [InlineData("NotedByLicense", 50)]
    public async Task A_value_that_is_too_long_is_refused(string field, int max)
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var service = CreateService(db);
        var data = new MedicalExamData(new Dictionary<string, string?> { [field] = new string('x', max + 1) });

        var result = await service.SaveAsync(Input(data));

        Assert.True(result.IsFailure);
        Assert.Empty(await db.LabReports.ToListAsync());
    }

    [Theory]
    [InlineData("HxAsthma", "Maybe")]
    [InlineData("RosEyes", "Y")]
    [InlineData("Classification", "E")]
    [InlineData("Dysmenorrhea", "Yes")]
    public async Task A_tick_that_is_not_one_of_its_options_is_refused(string field, string value)
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var service = CreateService(db);
        var data = new MedicalExamData(new Dictionary<string, string?> { [field] = value });

        var result = await service.SaveAsync(Input(data));

        Assert.True(result.IsFailure);
        Assert.Empty(await db.LabReports.ToListAsync());
    }

    [Fact]
    public async Task The_printout_has_every_field_by_name_and_five_signatories_with_licences_and_no_footer_note()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var service = CreateService(db);
        var saved = (await service.SaveAsync(Input())).Value;

        var print = (await service.GetPrintableAsync(saved.Id)).Value;

        Assert.Equal(ReportLayout.MedicalExamReport, print.Layout);
        Assert.Equal("Medical Examination Report", print.Title);
        Assert.Equal("v-Findings", print.Fields!["Findings"]);
        Assert.Equal("v-ContactNo", print.Fields["ContactNo"]);
        Assert.Equal(MedicalExamFields.All.Count, print.Fields.Count);
        Assert.Equal(string.Empty, print.FooterNote);
        Assert.Equal(["Vital Signs done by", "Interview done by", "Assessment done by", "Noted by", "Physician"], print.Signatories.Select(s => s.Role));
        Assert.Equal(["v-VitalSignsBy", "v-InterviewBy", "v-AssessmentBy", "v-NotedBy", "DR. MALONZO"], print.Signatories.Select(s => s.Name));
        Assert.Equal(["v-VitalSignsByLicense", "v-InterviewByLicense", "v-AssessmentByLicense", "v-NotedByLicense", "87844"], print.Signatories.Select(s => s.LicenseNo));
        Assert.Equal("Fit to work.", print.ResultTexts.Single(t => t.Label == "Remarks").Text);
        Assert.Equal("Rosa Dimaano", print.PatientLines.Single(l => l.Label == "Patient Name").Value);
    }

    [Fact]
    public async Task Printing_needs_the_print_permission_and_an_existing_result()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var service = CreateService(db);
        var saved = (await service.SaveAsync(Input())).Value;

        Assert.Equal("Medical Examination Report.NotFound", (await service.GetPrintableAsync(999)).Error.Code);

        _env.Session.SignIn(new AuthenticatedUser(7, "viewer", "Viewer", false, false, [new ModulePermission(ModuleIds.MedicalExamReport, false, false, false, false)]));
        Assert.Equal("Auth.Forbidden", (await service.GetPrintableAsync(saved.Id)).Error.Code);
    }
}
