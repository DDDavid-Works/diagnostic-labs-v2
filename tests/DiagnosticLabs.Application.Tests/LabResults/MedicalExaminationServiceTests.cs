using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Application.LabResults;
using DiagnosticLabs.Domain.Lab;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Application.Tests.LabResults;

public class MedicalExaminationServiceTests
{
    private readonly TestEnvironment _env = new();

    public MedicalExaminationServiceTests() => _env.Clock.UtcNow = new DateTime(2026, 10, 9, 4, 0, 0, DateTimeKind.Utc);

    private DateOnly Today => LocalTime.ToLocalDate(_env.Clock.UtcNow);

    private MedicalExaminationService CreateService(Infrastructure.Persistence.AppDbContext db) => new(db, _env.Session, _env.Clock);

    private void SignInAsAdmin() => _env.Session.SignIn(new AuthenticatedUser(1, "admin", "Admin", true, false, []));

    private LabResultHeader Header() => new(null, null, null, "Wow Wow", null, null, "ACME", Today, "www", null, null);

    private static MedicalExaminationData FullData() => new()
    {
        ContactNo = "34555", CivilStatus = "Single",
        ChestXray = new("N", "clear"), CBC = new("F", null), Urinalysis = new("N", null), Fecalysis = new("F", "ova"),
        HBsAg = new("N", null), DrugTest2Panel = new("F", null), DrugTest4Panel = new("N", "none"),
        Classification = "Fit", MedicalSurgicalHistory = "qqq", Assessment = "sss",
        AssessmentDoneBy = "DR. FIORA", PhysicianName = "DR. MALONZO", PhysicianLicense = "W2234O4504",
    };

    private MedicalExaminationInput Input(MedicalExaminationData? data = null) => new(0, Header(), data ?? FullData(), null);

    [Fact]
    public async Task Every_field_is_saved_and_read_back_in_the_medical_examination_tables()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var service = CreateService(db);

        var saved = (await service.SaveAsync(Input())).Value;
        var reopened = (await service.GetAsync(saved.Id)).Value;

        Assert.Equal(FullData(), reopened.Data);
        Assert.Equal("www", reopened.Header.Remarks);
        Assert.Equal(LabReportType.MedicalExamination, (await db.LabReports.AsNoTracking().SingleAsync()).ReportType);
        Assert.Equal("clear", (await db.Set<Domain.Lab.Reports.MedicalExaminationReport>().AsNoTracking().SingleAsync()).ChestXrayRemarks);
    }

    [Fact]
    public async Task A_result_other_than_N_or_F_and_an_unknown_classification_are_refused()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var service = CreateService(db);

        var badMark = await service.SaveAsync(Input(FullData() with { CBC = new("X", null) }));
        var badClass = await service.SaveAsync(Input(FullData() with { Classification = "E" }));

        Assert.True(badMark.IsFailure);
        Assert.True(badClass.IsFailure);
        Assert.Contains("Classification", badClass.Error.Message);
    }

    [Fact]
    public async Task Texts_that_are_too_long_are_refused()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var service = CreateService(db);

        var result = await service.SaveAsync(Input(FullData() with { Assessment = new string('x', 501) }));

        Assert.True(result.IsFailure);
        Assert.Contains("Assessment", result.Error.Message);
    }

    [Fact]
    public async Task The_printout_carries_the_marks_the_texts_and_the_signature_block()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var service = CreateService(db);
        var saved = (await service.SaveAsync(Input())).Value;

        var printable = (await service.GetPrintableAsync(saved.Id)).Value;

        Assert.Equal(ReportLayout.MedicalExamination, printable.Layout);
        Assert.Equal("Medical Examination Report", printable.Title);
        Assert.Equal("N", printable.Fields!["ChestXray"]);
        Assert.Equal("Fit", printable.Fields["Classification"]);
        Assert.Equal("DR. MALONZO", printable.Fields["PhysicianName"]);
        Assert.Equal("W2234O4504", printable.Fields["PhysicianLicense"]);
        Assert.Equal("qqq", printable.ResultTexts.Single(t => t.Label == "Medical/Surgical History").Text);
        Assert.Equal("www", printable.ResultTexts.Single(t => t.Label == "Remarks").Text);
    }
}
