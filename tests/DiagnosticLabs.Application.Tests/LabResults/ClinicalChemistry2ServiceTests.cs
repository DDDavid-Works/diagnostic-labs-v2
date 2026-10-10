using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Application.LabResults;
using DiagnosticLabs.Domain.Lab;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Application.Tests.LabResults;

public class ClinicalChemistry2ServiceTests
{
    private readonly TestEnvironment _env = new();

    public ClinicalChemistry2ServiceTests() => _env.Clock.UtcNow = new DateTime(2026, 10, 9, 4, 0, 0, DateTimeKind.Utc);

    private DateOnly Today => LocalTime.ToLocalDate(_env.Clock.UtcNow);

    private void SignInAsAdmin() => _env.Session.SignIn(new AuthenticatedUser(1, "admin", "Admin", true, false, []));

    private LabResultHeader Header() => new(null, null, null, "Armando Cruz", "45", "Male", null, Today, null, "DR. A", "DR. B");

    private static ClinicalChemistry2Data FullData() => new()
    {
        AlkalinePhosphataseConventional = new("APCNV", "APCU", "10"),
        AlkalinePhosphataseSystem = new("APSNV", "APSU", "11"),
        ASTSGOTConventional = new("ASTSGOTCNV", "ASTSGOTCU", "12"),
        ASTSGOTSystem = new("ASTSGOTSNV", "ASTSGOTSU", "13"),
    };

    private ClinicalChemistry2Input Input(ClinicalChemistry2Data? data = null) => new(0, Header(), data ?? FullData(), null);

    [Fact]
    public async Task Every_cell_is_saved_and_read_back()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var service = new ClinicalChemistry2Service(db, _env.Session, _env.Clock);

        var saved = (await service.SaveAsync(Input())).Value;
        var reopened = (await service.GetAsync(saved.Id)).Value;

        Assert.Equal(FullData(), reopened.Data);
        Assert.Equal(LabReportType.ClinicalChemistry2, (await db.LabReports.AsNoTracking().SingleAsync()).ReportType);
    }

    [Fact]
    public async Task A_value_that_is_too_long_is_refused()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var service = new ClinicalChemistry2Service(db, _env.Session, _env.Clock);

        var result = await service.SaveAsync(Input(FullData() with { ASTSGOTSystem = new(null, new string('x', ClinicalChemistry2Service.ValueMaxLength + 1), null) }));

        Assert.True(result.IsFailure);
        Assert.Contains("AST/SGOT (system unit) unit", result.Error.Message);
    }

    [Fact]
    public async Task The_printout_carries_every_cell_by_name()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var service = new ClinicalChemistry2Service(db, _env.Session, _env.Clock);
        var saved = (await service.SaveAsync(Input())).Value;

        var printable = (await service.GetPrintableAsync(saved.Id)).Value;

        Assert.Equal(ReportLayout.ClinicalChemistry2, printable.Layout);
        Assert.Equal("APCNV", printable.Fields!["AlkalinePhosphataseCNValue"]);
        Assert.Equal("13", printable.Fields["ASTSGOTSResults"]);
        Assert.Equal(12, printable.Fields.Count);
    }
}
