using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Application.LabResults;
using DiagnosticLabs.Domain.Lab;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Application.Tests.LabResults;

public class ClinicalChemistryServiceTests
{
    private readonly TestEnvironment _env = new();

    public ClinicalChemistryServiceTests() => _env.Clock.UtcNow = new DateTime(2026, 10, 9, 4, 0, 0, DateTimeKind.Utc);

    private DateOnly Today => LocalTime.ToLocalDate(_env.Clock.UtcNow);

    private ClinicalChemistryService CreateService(Infrastructure.Persistence.AppDbContext db) => new(db, _env.Session, _env.Clock);

    private void SignInAsAdmin() => _env.Session.SignIn(new AuthenticatedUser(1, "admin", "Admin", true, false, []));

    private LabResultHeader Header() => new(null, null, null, "Dolores Rivera", "28", "Female", null, Today, null, "DR. A", "DR. B");

    private static ClinicalChemistryData FullData() => new()
    {
        FBS = new("70-105", "9"), TotalCholesterol = new("up to 200", "8"), Triglycerides = new("44-148", "7"), HDL = new("30-75", "6"),
        BUN = new("7-18", "5"), Creatinine = new("0.40-1.40", "4"), BloodUricAcid = new("2.5-7.5", "3"), LDL = new("66-178", "2"), ALTSGPT = new("4-36", "1"),
    };

    private ClinicalChemistryInput Input(ClinicalChemistryData? data = null) => new(0, Header(), data ?? FullData(), null);

    [Fact]
    public async Task Every_line_is_saved_and_read_back()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var service = CreateService(db);

        var saved = (await service.SaveAsync(Input())).Value;
        var reopened = (await service.GetAsync(saved.Id)).Value;

        Assert.Equal(FullData(), reopened.Data);
        Assert.Equal(LabReportType.ClinicalChemistry, (await db.LabReports.AsNoTracking().SingleAsync()).ReportType);
    }

    [Fact]
    public async Task A_value_that_is_too_long_is_refused()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var service = CreateService(db);

        var result = await service.SaveAsync(Input(FullData() with { LDL = new(null, new string('x', ClinicalChemistryService.ValueMaxLength + 1)) }));

        Assert.True(result.IsFailure);
        Assert.Contains("LDL result", result.Error.Message);
    }

    [Fact]
    public async Task The_printout_carries_every_cell_by_name()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var service = CreateService(db);
        var saved = (await service.SaveAsync(Input())).Value;

        var printable = (await service.GetPrintableAsync(saved.Id)).Value;

        Assert.Equal(ReportLayout.ClinicalChemistry, printable.Layout);
        Assert.Equal("70-105", printable.Fields!["FBSNormalValue"]);
        Assert.Equal("1", printable.Fields["ALTSGPTResult"]);
        Assert.Equal(ClinicalChemistryService.Tests.Length * 2, printable.Fields.Count);
    }
}
