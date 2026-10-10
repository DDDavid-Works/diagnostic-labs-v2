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

    private LabResultHeader Header() => new(null, null, null, "Elena Castillo", "35", "Female", null, Today, "check again", "DR. A", "DR. B", MedicalTechnologist2: "DR. C");

    private static HematologyData FullData() => new()
    {
        Hematocrit = new("MALE: XXX    FEMALE: XXX", "0.45"),
        Hemoglobin = new("MALE: YYY    FEMALE: YYY", "140"),
        RBCCount = new("RRR", "4.8"),
        WBCCount = new("KKK", "7.5"),
        Neutrophils = new("UUU", "60"),
        Lymphocytes = new("HHH", "30"),
        Eosinophils = new("PPP", "2"),
        Monocytes = new("BBB", "5"),
        Basophils = new("FFF", "1"),
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

        var saved = (await service.SaveAsync(Input(new HematologyData { Hematocrit = new("  ", "  "), RBCCount = new(null, "3") }))).Value;

        Assert.Null(saved.Data.Hematocrit.NormalValue);
        Assert.Null(saved.Data.Hematocrit.Result);
        Assert.Equal(new NormalResultEntry(null, "3"), saved.Data.RBCCount);
    }

    [Fact]
    public async Task A_value_that_is_too_long_is_refused()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var service = CreateService(db);

        var result = await service.SaveAsync(Input(FullData() with { Hemoglobin = new(new string('x', HematologyService.ValueMaxLength + 1), null) }));

        Assert.True(result.IsFailure);
        Assert.Contains("Hemoglobin reference values", result.Error.Message);
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
        Assert.Equal("4.8", printable.Fields["RBCCountResult"]);
        Assert.Equal(["DR. A", "DR. C", "DR. B"], printable.Signatories.Select(s => s.Name));
        Assert.Equal(["* This is a validated and original report *", "This is an electronically signed document"], printable.FooterNote.Split('\n'));
    }
    [Fact]
    public async Task The_table_has_the_tests_of_the_printed_form_and_no_longer_has_stab()
    {
        Assert.Equal(
            ["Hematocrit", "Hemoglobin", "Red Blood Cell Count", "White Blood Cell Count", "Neutrophils", "Lymphocytes", "Eosinophils", "Monocytes", "Basophils", "Platelet Count"],
            HematologyService.Tests.Select(t => t.Label));
        await Task.CompletedTask;
    }

    [Fact]
    public async Task What_an_old_result_held_for_stab_is_kept_when_it_is_edited()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var service = CreateService(db);
        var saved = (await service.SaveAsync(Input())).Value;
        var row = await db.Set<DiagnosticLabs.Domain.Lab.Reports.HematologyReport>().SingleAsync();
        row.StabNValue = "0-5";
        row.StabResult = "2";
        await db.SaveChangesAsync();

        var edited = await service.SaveAsync(new HematologyInput(saved.Id, saved.Header, FullData() with { Hemoglobin = new("changed", "150") }, saved.RowVersion));

        Assert.True(edited.IsSuccess);
        var after = await db.Set<DiagnosticLabs.Domain.Lab.Reports.HematologyReport>().AsNoTracking().SingleAsync();
        Assert.Equal(("0-5", "2"), (after.StabNValue, after.StabResult));
        Assert.Equal("150", after.HemoglobinResult);
    }
}
