using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Application.LabResults;
using DiagnosticLabs.Domain.Lab;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Application.Tests.LabResults;

public class HematologyServiceTests
{
    private readonly TestEnvironment _env = new();

    public HematologyServiceTests() => _env.Clock.UtcNow = new DateTime(2026, 10, 9, 4, 0, 0, DateTimeKind.Utc);

    private DateOnly Today => LocalTime.ToLocalDate(_env.Clock.UtcNow);

    private HematologyService CreateService(Infrastructure.Persistence.AppDbContext db) => new(db, _env.Session, _env.Clock);

    private void SignInAsAdmin() => _env.Session.SignIn(new AuthenticatedUser(1, "admin", "Admin", true, false, []));

    private LabResultHeader Header() => new(null, null, null, "Elena Castillo", "35", "Female", null, Today, "check again", "DR. A", "DR. B");

    private static HematologyData FullData() => new()
    {
        Hematocrit = new("MALE: XXX    FEMALE: XXX", "0.45"),
        Hemoglobin = new("MALE: YYY    FEMALE: YYY", "140"),
        WBCCount = new("KKK", "7.5"),
        Segmenters = new("UUU", "60"),
        Lymphocytes = new("HHH", "30"),
        Eosinophils = new("PPP", "2"),
        Monocytes = new("BBB", "5"),
        Basophils = new("FFF", "1"),
        Stab = new("VVV", "2"),
        PlateletCount = new("DDD", "250"),
    };

    private HematologyInput Input(HematologyData? data = null) => new(0, Header(), data ?? FullData(), null);

    [Fact]
    public async Task Every_line_is_saved_and_read_back()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var service = CreateService(db);

        var saved = (await service.SaveAsync(Input())).Value;
        var reopened = (await service.GetAsync(saved.Id)).Value;

        Assert.Equal(FullData(), reopened.Data);
        Assert.Equal("check again", reopened.Header.Remarks);
        Assert.Equal(LabReportType.Hematology, (await db.LabReports.AsNoTracking().SingleAsync()).ReportType);
    }

    [Fact]
    public async Task Empty_values_are_kept_empty()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var service = CreateService(db);

        var saved = (await service.SaveAsync(Input(new HematologyData { Hematocrit = new("  ", "  "), Stab = new(null, "3") }))).Value;

        Assert.Null(saved.Data.Hematocrit.NormalValue);
        Assert.Null(saved.Data.Hematocrit.Result);
        Assert.Equal(new NormalResultEntry(null, "3"), saved.Data.Stab);
    }

    [Fact]
    public async Task A_value_that_is_too_long_is_refused()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var service = CreateService(db);

        var result = await service.SaveAsync(Input(FullData() with { Hemoglobin = new(new string('x', HematologyService.ValueMaxLength + 1), null) }));

        Assert.True(result.IsFailure);
        Assert.Contains("Hemoglobin normal values", result.Error.Message);
    }

    [Fact]
    public async Task The_printout_carries_every_cell_by_name()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var service = CreateService(db);
        var saved = (await service.SaveAsync(Input())).Value;

        var printable = (await service.GetPrintableAsync(saved.Id)).Value;

        Assert.Equal(ReportLayout.Hematology, printable.Layout);
        Assert.Equal("MALE: XXX    FEMALE: XXX", printable.Fields!["HematocritNormalValue"]);
        Assert.Equal("7.5", printable.Fields["WBCCountResult"]);
        Assert.Equal("250", printable.Fields["PlateletCountResult"]);
        Assert.Equal(HematologyService.Tests.Length * 2, printable.Fields.Count);
        Assert.Equal("check again", printable.ResultTexts.Single(t => t.Label == "Remarks").Text);
    }
}
