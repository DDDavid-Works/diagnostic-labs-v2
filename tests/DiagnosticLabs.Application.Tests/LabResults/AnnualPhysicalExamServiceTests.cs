using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Application.LabResults;
using DiagnosticLabs.Domain.Lab;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Application.Tests.LabResults;

public class AnnualPhysicalExamServiceTests
{
    private readonly TestEnvironment _env = new();

    public AnnualPhysicalExamServiceTests() => _env.Clock.UtcNow = new DateTime(2026, 10, 9, 4, 0, 0, DateTimeKind.Utc);

    private DateOnly Today => LocalTime.ToLocalDate(_env.Clock.UtcNow);

    private AnnualPhysicalExamService CreateService(Infrastructure.Persistence.AppDbContext db) => new(db, _env.Session, _env.Clock);

    private void SignInAsAdmin() => _env.Session.SignIn(new AuthenticatedUser(1, "admin", "Admin", true, false, []));

    private LabResultHeader Header() => new(null, null, null, "Emma Millstein", "26 years old", "Female", "WALK-IN", Today, null, "DR. A", "DR. B");

    private static AnnualPhysicalExamData FullData() => new()
    {
        DepartmentOrAgency = "Accounting", BirthDate = new DateOnly(2000, 2, 5), CivilStatus = "Single", ContactNo = "0438945984",
        ENT = "EENT 002", Gastroenterology = "GI 1", Respiratory = "RESP 1", IntegumentarySkin = "INTEG 002", Cardiology = "CARDIO 1",
        Psychology = "PSYCH 003", Endocrinology = "ENDO 001", OBGyneUrology = "OB 1", Muscoloskeletal = "MSK 1", InfectiousCommunicable = "INF 1",
        Neurological = "NEURO 1", Surgical = "SURG 1", OthersPast = "others 001", Medications = "meds 0001", ReviewOfSystems = "ros 001", Allergies = "alles 0001",
        IsSmoking = true, SmokingSinceWhen = "1", NumberOfSticksPerDay = 5, IsDrinking = true, DrinkingSinceWhen = "3", NumberOfBottles = 2, DrinkingFrequency = "Weekly",
        LMP = "5", LMPType = "Regular", BP1st = "1", BP2nd = "2", CardiacRate1st = "3", CardiacRate2nd = "4", Height = "5", Weight = "120", BMICategory = "BMI 001",
        VARightEyeWGlasses = "20/20", VARightEyeWOGlasses = "20/30", VALeftEyeWGlasses = "20/20", VALeftEyeWOGlasses = "20/40", VisualAcuity = "EOR",
        Skin = "N", HeadScalp = "F", Eyes = "N", Ears = "F", Nose = "N", TeethTonsilsThroatPharynx = "N", NeckLymphNodesThyroid = "F", ThoraxBreast = "N",
        HeartLungs = "F", AbdomenLiverSpleen = "N", InguinalAreaGenitalsAnus = "N", ExtremetiesSpine = "F", Tattoo = "N", MassCyst = "N", OthersPE = "N",
        Findings = "findings 001", VitalSignsBy = "NURSE A", HeightWeightBy = "NURSE B",
    };

    private AnnualPhysicalExamInput Input(AnnualPhysicalExamData? data = null) => new(0, Header(), data ?? FullData(), null);

    [Fact]
    public async Task Every_field_is_saved_and_read_back()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var service = CreateService(db);

        var saved = (await service.SaveAsync(Input())).Value;
        var reopened = (await service.GetAsync(saved.Id)).Value;

        Assert.Equal(FullData(), reopened.Data);
        Assert.Equal(new DateOnly(2000, 2, 5), reopened.Data.BirthDate);
        Assert.Equal(LabReportType.AnnualPhysicalExam, (await db.LabReports.AsNoTracking().SingleAsync()).ReportType);
    }

    [Fact]
    public async Task Smoking_and_drinking_details_are_kept_only_for_people_who_do()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var service = CreateService(db);

        var no = (await service.SaveAsync(Input(FullData() with { IsSmoking = false, IsDrinking = false }))).Value;

        Assert.False(no.Data.IsSmoking);
        Assert.Null(no.Data.SmokingSinceWhen);
        Assert.Null(no.Data.NumberOfSticksPerDay);
        Assert.False(no.Data.IsDrinking);
        Assert.Null(no.Data.DrinkingSinceWhen);
        Assert.Null(no.Data.NumberOfBottles);
        Assert.Null(no.Data.DrinkingFrequency);
    }

    [Fact]
    public async Task Nothing_chosen_stays_nothing()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var service = CreateService(db);

        var empty = (await service.SaveAsync(Input(new AnnualPhysicalExamData()))).Value;

        Assert.Null(empty.Data.IsSmoking);
        Assert.Null(empty.Data.LMPType);
        Assert.Null(empty.Data.Skin);
        Assert.Null(empty.Data.VitalSignsBy);
        Assert.Null(empty.Data.BirthDate);
    }

    [Fact]
    public async Task The_physical_examination_takes_only_N_or_F_and_the_choices_are_checked()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var service = CreateService(db);

        var badPe = await service.SaveAsync(Input(FullData() with { Skin = "X" }));
        var badFrequency = await service.SaveAsync(Input(FullData() with { DrinkingFrequency = "Hourly" }));
        var badLmp = await service.SaveAsync(Input(FullData() with { LMPType = "Odd" }));
        var badAcuity = await service.SaveAsync(Input(FullData() with { VisualAcuity = "Blurry" }));
        var negative = await service.SaveAsync(Input(FullData() with { NumberOfBottles = -1 }));
        var tooLong = await service.SaveAsync(Input(FullData() with { Medications = new string('x', 201) }));
        var findingsLong = await service.SaveAsync(Input(FullData() with { Findings = new string('x', 501) }));

        Assert.All([badPe, badFrequency, badLmp, badAcuity, negative, tooLong, findingsLong], r => Assert.True(r.IsFailure));
        Assert.Contains("Skin", badPe.Error.Message, StringComparison.Ordinal);
        Assert.Contains("Medications", tooLong.Error.Message, StringComparison.Ordinal);
        Assert.Empty(await db.LabReports.ToListAsync());
    }

    [Fact]
    public async Task Annual_physical_exams_are_kept_apart_from_other_result_types()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var ape = CreateService(db);
        var stool = new StoolFecalysisService(db, _env.Session, _env.Clock);
        await ape.SaveAsync(Input());
        await stool.SaveAsync(new StoolFecalysisInput(0, Header(), "BROWN", null, "NORMAL", null));

        Assert.Single((await ape.SearchAsync(new CrudSearch(null))).Value.Items);
        Assert.Single((await stool.SearchAsync(new CrudSearch(null))).Value.Items);
    }
}