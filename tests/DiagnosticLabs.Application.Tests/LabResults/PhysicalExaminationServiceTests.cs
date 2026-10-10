using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Board;
using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Application.LabResults;
using DiagnosticLabs.Domain.Lab;
using DiagnosticLabs.Domain.Lab.Reports;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Application.Tests.LabResults;

public class PhysicalExaminationServiceTests
{
    private readonly TestEnvironment _env = new();

    public PhysicalExaminationServiceTests() => _env.Clock.UtcNow = new DateTime(2026, 10, 9, 4, 0, 0, DateTimeKind.Utc);

    private DateOnly Today => LocalTime.ToLocalDate(_env.Clock.UtcNow);

    private PhysicalExaminationService CreateService(Infrastructure.Persistence.AppDbContext db) => new(db, _env.Session, _env.Clock);

    private void SignInAsAdmin() => _env.Session.SignIn(new AuthenticatedUser(1, "admin", "Admin", true, false, []));

    private LabResultHeader Header() => new(null, null, null, "Rosa Dimaano", "41", "Female", "ACME", Today, null, "DR. A", "DR. B");

    private static IEnumerable<string> AllTextFields() => PhysicalExaminationFields.TextFields().Select(f => f.Name);

    // Every field gets its own value, so a value that lands in the wrong column shows.
    private PhysicalExaminationData FullData() => new(
        PhysicalExaminationFields.Dates.Select((n, i) => (n, Date: (DateOnly?)Today.AddDays(-i))).ToDictionary(x => x.n, x => x.Date),
        AllTextFields().ToDictionary(n => n, n => (string?)("v-" + n)));

    private PhysicalExaminationInput Input(PhysicalExaminationData? data = null) => new(0, Header(), data ?? FullData(), null);

    [Fact]
    public void Every_field_of_the_form_is_a_column_of_the_report()
    {
        // The service reads and writes the fields by name, so a name without a column would fail the moment the service is first used.
        var columns = typeof(PhysicalExaminationReport).GetProperties().Select(p => p.Name).ToHashSet();

        Assert.All(PhysicalExaminationFields.Dates.Concat(AllTextFields()), name => Assert.Contains(name, columns));
        Assert.Equal(PhysicalExaminationFields.Dates.Length + AllTextFields().Count(), columns.Count - 2 /* LabReportId, LabReport */);
    }

    [Fact]
    public void The_module_is_wired_in_like_the_other_result_forms()
    {
        Assert.Equal(LabReportType.PhysicalExamination, LabResultModules.ReportTypeOf(ModuleIds.PhysicalExamination));
    }

    [Fact]
    public async Task Every_field_and_date_is_saved_and_read_back_in_its_own_place()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var service = CreateService(db);

        var saved = (await service.SaveAsync(Input())).Value;
        var reopened = (await service.GetAsync(saved.Id)).Value;

        foreach (var name in AllTextFields())
            Assert.Equal("v-" + name, reopened.Data.Value(name));

        for (var i = 0; i < PhysicalExaminationFields.Dates.Length; i++)
            Assert.Equal(Today.AddDays(-i), reopened.Data.Date(PhysicalExaminationFields.Dates[i]));

        Assert.Equal(LabReportType.PhysicalExamination, (await db.LabReports.AsNoTracking().SingleAsync()).ReportType);
    }

    [Fact]
    public async Task A_new_form_starts_with_empty_dates_and_a_section_may_be_left_out()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var service = CreateService(db);

        var saved = (await service.SaveAsync(Input(PhysicalExaminationData.Empty))).Value;

        Assert.All(PhysicalExaminationFields.Dates, n => Assert.Null(saved.Data.Date(n)));
        Assert.All(AllTextFields(), n => Assert.Null(saved.Data.Value(n)));
    }

    [Fact]
    public async Task Blank_values_are_kept_empty()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var service = CreateService(db);
        var data = new PhysicalExaminationData(new Dictionary<string, DateOnly?>(), new Dictionary<string, string?> { ["BloodTyping"] = "   ", ["Others"] = "  noted  " });

        var saved = (await service.SaveAsync(Input(data))).Value;

        Assert.Null(saved.Data.Value("BloodTyping"));
        Assert.Equal("noted", saved.Data.Value("Others"));
    }

    [Theory]
    [InlineData("RhTyping", PhysicalExaminationFields.ChoiceMaxLength)]
    [InlineData("HematocritResult", PhysicalExaminationFields.CellMaxLength)]
    [InlineData("Others", PhysicalExaminationFields.TextMaxLength)]
    [InlineData("FecalysisResult", PhysicalExaminationFields.TextMaxLength)]
    public async Task A_value_that_is_too_long_is_refused(string field, int max)
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var service = CreateService(db);
        var data = new PhysicalExaminationData(new Dictionary<string, DateOnly?>(), new Dictionary<string, string?> { [field] = new string('x', max + 1) });

        var result = await service.SaveAsync(Input(data));

        Assert.True(result.IsFailure);
        Assert.Empty(await db.LabReports.ToListAsync());
    }

    [Fact]
    public async Task The_printout_carries_every_field_by_name_with_the_dates_formatted_and_two_signatories_without_licences()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var service = CreateService(db);
        var saved = (await service.SaveAsync(Input())).Value;

        var print = (await service.GetPrintableAsync(saved.Id)).Value;

        Assert.Equal(ReportLayout.PhysicalExamination, print.Layout);
        Assert.Equal("Laboratory Results", print.Title);
        Assert.Equal("v-HematocritFemaleNValue", print.Fields!["HematocritFemaleNValue"]);
        Assert.Equal("v-UrineAmorphousUratesPO4", print.Fields["UrineAmorphousUratesPO4"]);
        Assert.Equal(Today.ToString("d", System.Globalization.CultureInfo.CurrentCulture), print.Fields["CbcDate"]);
        Assert.Equal(PhysicalExaminationFields.Dates.Length + AllTextFields().Count(), print.Fields.Count);
        Assert.Equal(["Medical Technologist", "Pathologist"], print.Signatories.Select(s => s.Role));
        Assert.Equal(["DR. A", "DR. B"], print.Signatories.Select(s => s.Name));
        Assert.All(print.Signatories, s => Assert.Null(s.LicenseNo));
    }

    [Fact]
    public async Task Printing_needs_the_print_permission_and_an_existing_result()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var service = CreateService(db);
        var saved = (await service.SaveAsync(Input())).Value;

        Assert.Equal("Physical Examination.NotFound", (await service.GetPrintableAsync(999)).Error.Code);

        _env.Session.SignIn(new AuthenticatedUser(7, "viewer", "Viewer", false, false, [new ModulePermission(ModuleIds.PhysicalExamination, false, false, false, false)]));
        Assert.Equal("Auth.Forbidden", (await service.GetPrintableAsync(saved.Id)).Error.Code);
    }
}
