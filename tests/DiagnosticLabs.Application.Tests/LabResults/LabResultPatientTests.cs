using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.LabResults;
using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Domain.Patients;
using DiagnosticLabs.Domain.Registrations;
using DiagnosticLabs.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Application.Tests.LabResults;

/// <summary>A result made without a registration can still be tied to a patient picked from the patient list.</summary>
public class LabResultPatientTests
{
    private readonly TestEnvironment _env = new();

    public LabResultPatientTests() => _env.Clock.UtcNow = new DateTime(2026, 10, 9, 4, 0, 0, DateTimeKind.Utc);

    private DateOnly Today => LocalTime.ToLocalDate(_env.Clock.UtcNow);

    private void SignInAsAdmin() => _env.Session.SignIn(new AuthenticatedUser(1, "admin", "Admin", true, false, []));

    private static async Task<Patient> SeedPatientAsync(AppDbContext db, string name = "Laura Arseid")
    {
        var patient = new Patient { PatientCode = "2026-00000003", PatientName = name, DateOfBirth = new DateOnly(2000, 2, 8), Sex = "Female" };
        db.Patients.Add(patient);
        await db.SaveChangesAsync();
        return patient;
    }

    private LabResultHeader Header(long? registrationId = null, long? patientId = null) =>
        new(registrationId, null, "2026-00000003", "Laura Arseid", "26 years old", "Female", null, Today, null, null, null, false, patientId);

    [Fact]
    public async Task A_picked_patient_is_linked_to_the_result_without_a_registration()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var patient = await SeedPatientAsync(db);
        var service = new StoolFecalysisService(db, _env.Session, _env.Clock);

        var saved = (await service.SaveAsync(new StoolFecalysisInput(0, Header(null, patient.Id), "BROWN", null, "NORMAL", null))).Value;
        var row = await db.LabReports.AsNoTracking().SingleAsync();

        Assert.Equal(patient.Id, row.PatientId);
        Assert.Null(row.PatientRegistrationId);
        Assert.Equal(patient.Id, saved.Header.PatientId);
        Assert.Equal(patient.Id, (await service.GetAsync(saved.Id)).Value.Header.PatientId);
    }

    [Fact]
    public async Task With_a_registration_the_registrations_patient_wins_over_a_picked_one()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var other = await SeedPatientAsync(db, "Someone Else");
        var registration = new PatientRegistration
        {
            RegistrationCode = "R-1", InputDate = _env.Clock.UtcNow, Patient = new Patient { PatientCode = "P-9", PatientName = "Registered Person" },
        };
        db.PatientRegistrations.Add(registration);
        await db.SaveChangesAsync();
        var service = new StoolFecalysisService(db, _env.Session, _env.Clock);

        await service.SaveAsync(new StoolFecalysisInput(0, Header(registration.Id, other.Id), null, null, "NORMAL", null));

        Assert.Equal(registration.PatientId, (await db.LabReports.AsNoTracking().SingleAsync()).PatientId);
    }

    [Fact]
    public async Task A_patient_that_no_longer_exists_is_refused()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var service = new StoolFecalysisService(db, _env.Session, _env.Clock);

        var result = await service.SaveAsync(new StoolFecalysisInput(0, Header(null, 999), null, null, "NORMAL", null));

        Assert.True(result.IsFailure);
        Assert.Contains("patient no longer exists", result.Error.Message, StringComparison.Ordinal);
        Assert.Empty(await db.LabReports.ToListAsync());
    }

    [Fact]
    public async Task Patients_are_offered_by_name_or_code_and_load_with_their_age()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var patient = await SeedPatientAsync(db);
        await SeedPatientAsync(db, "Another Person");
        var lookup = new LabRegistrationLookup(db, _env.Session, _env.Clock);

        var byName = (await lookup.SuggestPatientsAsync(ModuleIds.StoolFecalysis, "Arse")).Value;
        var tooShort = (await lookup.SuggestPatientsAsync(ModuleIds.StoolFecalysis, "A")).Value;
        var loaded = (await lookup.GetPatientAsync(ModuleIds.StoolFecalysis, patient.Id)).Value;

        Assert.Equal("Laura Arseid", Assert.Single(byName).PatientName);
        Assert.Equal(2, (await lookup.SuggestPatientsAsync(ModuleIds.StoolFecalysis, "2026-0000")).Value.Count);
        Assert.Empty(tooShort);
        Assert.Equal("26 years old", loaded.Age);
        Assert.Equal("Female", loaded.Sex);
        Assert.Equal("Patient.NotFound", (await lookup.GetPatientAsync(ModuleIds.StoolFecalysis, 999)).Error.Code);
    }

    [Fact]
    public async Task The_patient_lookup_needs_the_screen_permission()
    {
        await using var db = _env.CreateDb();
        await SeedPatientAsync(db);
        _env.Session.SignIn(new AuthenticatedUser(8, "nobody", "Nobody", false, false, []));
        var lookup = new LabRegistrationLookup(db, _env.Session, _env.Clock);

        Assert.Equal("Auth.Forbidden", (await lookup.SuggestPatientsAsync(ModuleIds.StoolFecalysis, "Arse")).Error.Code);
        Assert.Equal("Auth.Forbidden", (await lookup.GetPatientAsync(ModuleIds.StoolFecalysis, 1)).Error.Code);
    }
}