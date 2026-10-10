using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Application.LabResults;
using DiagnosticLabs.Domain.Lab;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Application.Tests.LabResults;

public class UrinalysisServiceTests
{
    private readonly TestEnvironment _env = new();

    public UrinalysisServiceTests() => _env.Clock.UtcNow = new DateTime(2026, 10, 9, 4, 0, 0, DateTimeKind.Utc);

    private DateOnly Today => LocalTime.ToLocalDate(_env.Clock.UtcNow);

    private UrinalysisService CreateService(Infrastructure.Persistence.AppDbContext db) => new(db, _env.Session, _env.Clock);

    private LabResultHeader Header() => new(null, null, null, "Isola Millstein", "26 years old", "Female", null, Today, "REMARKS XXX", "DR. MICHAUD", "DR. FIORA");

    private UrinalysisInput Input(long id = 0, byte[]? rowVersion = null) => new(
        id, Header(), "GREEN", "NORMAL", "1", "2", "3", "4", "5", "6", "7", "8", "9", "A", "B", "C", "OTHERS XXXX", rowVersion);

    private void SignInAsAdmin() => _env.Session.SignIn(new AuthenticatedUser(1, "admin", "Admin", true, false, []));

    [Fact]
    public async Task Every_field_is_saved_and_read_back()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var service = CreateService(db);

        var saved = (await service.SaveAsync(Input())).Value;
        var reopened = (await service.GetAsync(saved.Id)).Value;

        Assert.Equal(["GREEN", "NORMAL", "1", "2", "3", "4", "5", "6", "7", "8", "9", "A", "B", "C"],
            new string?[] { reopened.Color, reopened.Appearance, reopened.Reaction, reopened.SPGravity, reopened.Protein, reopened.Glucose, reopened.PusCells,
                    reopened.RedCells, reopened.MucusThreads, reopened.EpithelialCells, reopened.AmorphousUratesPO4, reopened.Bacteria, reopened.Casts, reopened.Crystals }.Select(v => v ?? string.Empty).ToArray());
        Assert.Equal("OTHERS XXXX", reopened.Others);
        Assert.Equal("REMARKS XXX", reopened.Header.Remarks);
        Assert.Equal(LabReportType.Urinalysis, (await db.LabReports.AsNoTracking().SingleAsync()).ReportType);
    }

    [Fact]
    public async Task Urinalysis_results_are_kept_apart_from_stool_results()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var urinalysis = CreateService(db);
        var stool = new StoolFecalysisService(db, _env.Session, _env.Clock);
        await urinalysis.SaveAsync(Input());
        await stool.SaveAsync(new StoolFecalysisInput(0, Header(), "BROWN", "FORMED", "NORMAL", null));

        Assert.Single((await urinalysis.SearchAsync(new CrudSearch(null))).Value.Items);
        Assert.Single((await stool.SearchAsync(new CrudSearch(null))).Value.Items);
    }

    [Fact]
    public async Task Blank_values_are_stored_as_nothing_and_long_ones_are_refused()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var service = CreateService(db);

        var blank = (await service.SaveAsync(Input() with { Color = "  ", Others = null })).Value;
        var tooLong = await service.SaveAsync(Input() with { Glucose = new string('x', 51) });
        var othersTooLong = await service.SaveAsync(Input() with { Others = new string('x', 501) });

        Assert.Null(blank.Color);
        Assert.Equal(string.Empty, blank.Others);
        Assert.Contains("Glucose", tooLong.Error.Message, StringComparison.Ordinal);
        Assert.Contains("Others", othersTooLong.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_printout_lists_the_fields_in_the_order_of_the_printed_form()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var service = CreateService(db);
        var saved = (await service.SaveAsync(Input())).Value;

        var print = (await service.GetPrintableAsync(saved.Id)).Value;

        Assert.Equal(ReportLayout.Urinalysis, print.Layout);
        Assert.Equal(
            ["Color", "Appearance", "Reaction (PH)", "SP. Gravity", "Protein", "Glucose", "Pus Cells", "Red Cells",
             "Mucus Threads", "Epithelial Cells", "Amorphous Urates / PO4", "Bacteria", "Casts", "Crystals"],
            print.ResultLines.Select(l => l.Label));
        Assert.Equal("GREEN", print.ResultLines[0].Value);
        Assert.Equal("C", print.ResultLines[13].Value);
        Assert.Equal(["Others", "Remarks"], print.ResultTexts.Select(t => t.Label));
        Assert.Equal("OTHERS XXXX", print.ResultTexts[0].Text);
        Assert.Equal(["DR. MICHAUD", "DR. FIORA"], print.Signatories.Select(s => s.Name));
    }

    [Fact]
    public async Task A_second_urinalysis_for_a_registration_needs_confirmation_but_a_stool_result_does_not_count()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var registration = new Domain.Registrations.PatientRegistration
        {
            RegistrationCode = "R-1", InputDate = _env.Clock.UtcNow, Patient = new Domain.Patients.Patient { PatientCode = "P-1", PatientName = "Isola" },
        };
        db.PatientRegistrations.Add(registration);
        await db.SaveChangesAsync();
        var service = CreateService(db);
        var stool = new StoolFecalysisService(db, _env.Session, _env.Clock);
        var linked = Header() with { RegistrationId = registration.Id };
        await stool.SaveAsync(new StoolFecalysisInput(0, linked, null, null, "NORMAL", null));

        var first = await service.SaveAsync(Input() with { Header = linked });
        var second = await service.SaveAsync(Input() with { Header = linked });
        var confirmed = await service.SaveAsync(Input() with { Header = linked with { ConfirmedDuplicate = true } });

        Assert.True(first.IsSuccess);
        Assert.Equal("LabResult.Duplicate", second.Error.Code);
        Assert.True(confirmed.IsSuccess);
    }
}