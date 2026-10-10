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

    private LabResultHeader Header() =>
        new(null, null, null, "Dolores Rivera", "28", "Female", null, Today, null, "DR. A", "DR. B", MedicalTechnologist2: "DR. C");

    // Every test gets its own distinct six values, so a value landing in the wrong column shows.
    private static ChemistryTestEntry EntryOf(int i) => new(
        new UnitResultEntry($"cn{i}", $"cu{i}", $"cr{i}"),
        new UnitResultEntry($"sn{i}", $"su{i}", $"sr{i}"));

    private static ClinicalChemistryData FullData() =>
        new(ClinicalChemistryService.Tests.Select((t, i) => (t.Name, Entry: EntryOf(i))).ToDictionary(x => x.Name, x => x.Entry));

    private ClinicalChemistryInput Input(ClinicalChemistryData? data = null) => new(0, Header(), data ?? FullData(), null);

    [Fact]
    public async Task The_table_has_the_ten_tests_of_the_printed_form_in_order()
    {
        Assert.Equal(
            ["Fasting Blood Sugar", "Cholesterol", "Triglycerides", "HDL", "LDL", "Creatinine", "Blood Urea Nitrogen", "Blood Uric Acid", "ALT/SGPT", "AST/SGOT"],
            ClinicalChemistryService.Tests.Select(t => t.Label));
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Every_cell_is_saved_and_read_back_in_its_own_place()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var service = CreateService(db);

        var saved = (await service.SaveAsync(Input())).Value;
        var reopened = (await service.GetAsync(saved.Id)).Value;

        for (var i = 0; i < ClinicalChemistryService.Tests.Length; i++)
            Assert.Equal(EntryOf(i), reopened.Data.Of(ClinicalChemistryService.Tests[i].Name));

        Assert.Equal(LabReportType.ClinicalChemistry, (await db.LabReports.AsNoTracking().SingleAsync()).ReportType);
        Assert.Equal("DR. C", reopened.Header.MedicalTechnologist2);
    }

    [Fact]
    public async Task A_test_left_out_is_empty()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var service = CreateService(db);

        var saved = (await service.SaveAsync(Input(ClinicalChemistryData.Empty))).Value;

        Assert.All(ClinicalChemistryService.Tests, t => Assert.Equal(ChemistryTestEntry.Empty, saved.Data.Of(t.Name)));
    }

    [Fact]
    public async Task A_value_that_is_too_long_is_refused()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var service = CreateService(db);
        var tooLong = new ChemistryTestEntry(
            new UnitResultEntry(null, null, null), new UnitResultEntry(null, new string('x', ClinicalChemistryService.ValueMaxLength + 1), null));
        var data = new ClinicalChemistryData(new Dictionary<string, ChemistryTestEntry> { ["LDL"] = tooLong });

        var result = await service.SaveAsync(Input(data));

        Assert.True(result.IsFailure);
        Assert.Contains("LDL S.I. unit", result.Error.Message);
    }

    [Fact]
    public async Task The_printout_carries_every_cell_by_name_and_has_three_signatories_and_remarks()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var service = CreateService(db);
        var header = Header() with { Remarks = "Fasting for 8 hours", MedicalTechnologistLicense = "111", MedicalTechnologist2License = "222", PathologistLicense = "333" };
        var saved = (await service.SaveAsync(new ClinicalChemistryInput(0, header, FullData(), null))).Value;

        var printable = (await service.GetPrintableAsync(saved.Id)).Value;

        Assert.Equal(ReportLayout.ClinicalChemistry, printable.Layout);
        Assert.Equal("cn0", printable.Fields!["FastingBloodSugarCNValue"]);
        Assert.Equal("sr9", printable.Fields["ASTSGOTSResults"]);
        Assert.Equal(ClinicalChemistryService.Tests.Length * ClinicalChemistryService.Suffixes.Length, printable.Fields.Count);
        Assert.Equal(["DR. A", "DR. C", "DR. B"], printable.Signatories.Select(s => s.Name));
        Assert.Equal(["111", "222", "333"], printable.Signatories.Select(s => s.LicenseNo));
        Assert.Equal("Fasting for 8 hours", printable.ResultTexts.Single(t => t.Label == "Remarks").Text);
        Assert.Equal(["* This is a validated and original report *", "This is an electronically signed document"], printable.FooterNote.Split('\n'));
    }
}
