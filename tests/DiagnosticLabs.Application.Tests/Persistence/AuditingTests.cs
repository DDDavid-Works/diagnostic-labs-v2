using System.Text.Json;
using DiagnosticLabs.Domain.Auditing;
using DiagnosticLabs.Domain.Patients;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Application.Tests.Persistence;

public class AuditingTests
{
    private readonly TestEnvironment _env = new();

    private static Patient NewPatient(string code = "P-001") => new()
    {
        PatientCode = code,
        PatientName = "Jane Roe",
        Address = "Somewhere",
    };

    [Fact]
    public async Task Insert_stamps_utc_time_and_user()
    {
        await using var db = _env.CreateDb();
        _env.SignInAs(42);
        var patient = NewPatient();

        db.Patients.Add(patient);
        await db.SaveChangesAsync();

        Assert.Equal(_env.Clock.UtcNow, patient.CreatedAtUtc);
        Assert.Equal(42, patient.CreatedByUserId);
        Assert.Equal(42, patient.UpdatedByUserId);
    }

    [Fact]
    public async Task Unauthenticated_changes_are_attributed_to_the_system_not_user_zero()
    {
        await using var db = _env.CreateDb();
        var patient = NewPatient();

        db.Patients.Add(patient);
        await db.SaveChangesAsync();

        Assert.Null(patient.CreatedByUserId);
    }

    [Fact]
    public async Task Update_keeps_creator_and_restamps_updater()
    {
        await using var db = _env.CreateDb();
        _env.SignInAs(1);
        var patient = NewPatient();
        db.Patients.Add(patient);
        await db.SaveChangesAsync();

        _env.SignInAs(2);
        _env.Clock.UtcNow += TimeSpan.FromHours(1);
        patient.PatientName = "Jane Doe";
        await db.SaveChangesAsync();

        Assert.Equal(1, patient.CreatedByUserId);
        Assert.Equal(2, patient.UpdatedByUserId);
        Assert.Equal(_env.Clock.UtcNow, patient.UpdatedAtUtc);
        Assert.True(patient.UpdatedAtUtc > patient.CreatedAtUtc);
    }

    [Fact]
    public async Task Removing_a_patient_soft_deletes_it_and_hides_it()
    {
        await using var db = _env.CreateDb();
        _env.SignInAs(7);
        var patient = NewPatient();
        db.Patients.Add(patient);
        await db.SaveChangesAsync();

        db.Patients.Remove(patient);
        await db.SaveChangesAsync();

        Assert.Empty(await db.Patients.ToListAsync());
        var stored = await db.Patients.IgnoreQueryFilters().SingleAsync();
        Assert.True(stored.IsDeleted);
        Assert.Equal(7, stored.DeletedByUserId);
        Assert.Equal(_env.Clock.UtcNow, stored.DeletedAtUtc);
    }

    [Fact]
    public async Task Every_change_is_written_to_the_audit_log()
    {
        await using var db = _env.CreateDb();
        _env.SignInAs(5);
        var patient = NewPatient();
        db.Patients.Add(patient);
        await db.SaveChangesAsync();

        patient.PatientName = "Jane Doe";
        await db.SaveChangesAsync();

        db.Patients.Remove(patient);
        await db.SaveChangesAsync();

        var logs = await db.AuditLogs.OrderBy(a => a.Id).ToListAsync();
        Assert.Equal([AuditAction.Insert, AuditAction.Update, AuditAction.Delete], logs.Select(l => l.Action));
        Assert.All(logs, l =>
        {
            Assert.Equal(nameof(Patient), l.EntityName);
            Assert.Equal(patient.Id.ToString(), l.EntityId);
            Assert.Equal(5, l.ChangedByUserId);
        });

        var update = logs[1];
        using var oldValues = JsonDocument.Parse(update.OldValues!);
        using var newValues = JsonDocument.Parse(update.NewValues!);
        Assert.Equal("Jane Roe", oldValues.RootElement.GetProperty("PatientName").GetString());
        Assert.Equal("Jane Doe", newValues.RootElement.GetProperty("PatientName").GetString());
        Assert.False(newValues.RootElement.TryGetProperty("UpdatedAtUtc", out _));
    }

    [Fact]
    public async Task Password_hashes_never_reach_the_audit_log()
    {
        await using var db = _env.CreateDb();
        db.Users.Add(TestEnvironment.NewUser("alice", "super-secret-hash"));
        await db.SaveChangesAsync();

        var log = await db.AuditLogs.SingleAsync();
        Assert.DoesNotContain("super-secret-hash", log.NewValues, StringComparison.Ordinal);
        Assert.Contains("[redacted]", log.NewValues, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Saving_without_changes_writes_no_audit_rows()
    {
        await using var db = _env.CreateDb();
        db.Patients.Add(NewPatient());
        await db.SaveChangesAsync();
        var before = await db.AuditLogs.CountAsync();

        await db.SaveChangesAsync();

        Assert.Equal(before, await db.AuditLogs.CountAsync());
    }
}
