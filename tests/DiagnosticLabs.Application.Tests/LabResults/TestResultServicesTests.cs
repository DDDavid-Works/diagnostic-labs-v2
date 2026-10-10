using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Application.LabResults;
using DiagnosticLabs.Domain.Lab;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Application.Tests.LabResults;

public class TestResultServicesTests
{
    private readonly TestEnvironment _env = new();

    public TestResultServicesTests() => _env.Clock.UtcNow = new DateTime(2026, 10, 9, 4, 0, 0, DateTimeKind.Utc);

    private DateOnly Today => LocalTime.ToLocalDate(_env.Clock.UtcNow);

    private void SignInAsAdmin() => _env.Session.SignIn(new AuthenticatedUser(1, "admin", "Admin", true, false, []));

    private LabResultHeader Header() => new(null, null, null, "Jose Ramos", "44", "Male", null, Today, "GOOD", "DR. A", "DR. B");

    private TestResultInput Input(string? test = "Hepatitis B Screening", string? result = "POSITIVE") => new(0, Header(), test, result, null);

    [Fact]
    public async Task Serology_saves_the_test_and_result_and_reads_them_back()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var service = new SerologyService(db, _env.Session, _env.Clock);

        var saved = (await service.SaveAsync(Input())).Value;
        var reopened = (await service.GetAsync(saved.Id)).Value;

        Assert.Equal("Hepatitis B Screening", reopened.Test);
        Assert.Equal("POSITIVE", reopened.Result);
        Assert.Equal("GOOD", reopened.Header.Remarks);
        Assert.Equal(LabReportType.Serology, (await db.LabReports.AsNoTracking().SingleAsync()).ReportType);
    }

    [Fact]
    public async Task Immunology_is_kept_apart_from_serology()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var serology = new SerologyService(db, _env.Session, _env.Clock);
        var immunology = new ImmunologyService(db, _env.Session, _env.Clock);

        await serology.SaveAsync(Input("Hepatitis B Screening"));
        var saved = (await immunology.SaveAsync(Input("TEST 001", "QQQQ"))).Value;

        Assert.Equal(LabReportType.Immunology, (await db.LabReports.AsNoTracking().SingleAsync(r => r.Id == saved.Id)).ReportType);
        Assert.Single((await immunology.SearchAsync(new LabResultSearch(null, null, 1, 50, false))).Value.Items);
        Assert.Single((await serology.SearchAsync(new LabResultSearch(null, null, 1, 50, false))).Value.Items);
    }

    [Fact]
    public async Task An_empty_test_stays_empty_and_a_long_result_is_refused()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var service = new SerologyService(db, _env.Session, _env.Clock);

        Assert.Null((await service.SaveAsync(Input("  ", "x"))).Value.Test);

        var tooLong = await service.SaveAsync(Input(result: new string('x', TestResultService<DiagnosticLabs.Domain.Lab.Reports.SerologyReport>.ResultMaxLength + 1)));
        Assert.True(tooLong.IsFailure);
    }

    [Fact]
    public async Task The_printout_names_the_test_and_carries_the_result()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var service = new ImmunologyService(db, _env.Session, _env.Clock);
        var saved = (await service.SaveAsync(Input("TEST 001", "QQQQ"))).Value;

        var printable = (await service.GetPrintableAsync(saved.Id)).Value;

        Assert.Equal(ReportLayout.TestResult, printable.Layout);
        Assert.Equal("Immunology", printable.Title);
        Assert.Equal("TEST 001", printable.ResultLines.Single(l => l.Label == "Test").Value);
        Assert.Equal("QQQQ", printable.ResultTexts.Single(t => t.Label == "Result").Text);
        Assert.Equal("GOOD", printable.ResultTexts.Single(t => t.Label == "Remarks").Text);
    }

    [Fact]
    public async Task Pregnancy_test_keeps_only_its_result_and_prints_without_a_test_row()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var service = new PregnancyTestService(db, _env.Session, _env.Clock);

        var saved = (await service.SaveAsync(Input("ignored", "NEGATIVE"))).Value;
        var printable = (await service.GetPrintableAsync(saved.Id)).Value;

        Assert.Null(saved.Test);
        Assert.Equal("NEGATIVE", saved.Result);
        Assert.Equal(LabReportType.PregnancyTest, (await db.LabReports.AsNoTracking().SingleAsync()).ReportType);
        Assert.Empty(printable.ResultLines);
        Assert.Equal("NEGATIVE", printable.ResultTexts.Single(t => t.Label == "Result").Text);
    }
}