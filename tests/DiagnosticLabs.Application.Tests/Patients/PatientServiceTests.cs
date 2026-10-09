using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Codes;
using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Application.Entries;
using DiagnosticLabs.Application.Patients;
using DiagnosticLabs.Domain.Patients;
using DiagnosticLabs.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Application.Tests.Patients;

public class PatientServiceTests
{
    private readonly TestEnvironment _env = new();

    private PatientService CreateService(AppDbContext db) =>
        new(db, _env.Session, new CodeGenerator(db, _env.Clock), _env.Clock);

    private void SignIn(params ModuleAction[] allowed) =>
        _env.Session.SignIn(new AuthenticatedUser(
            10, "clerk", "Clerk", false, false,
            [new ModulePermission(
                ModuleIds.Patients,
                allowed.Contains(ModuleAction.Create),
                allowed.Contains(ModuleAction.Edit),
                allowed.Contains(ModuleAction.Delete),
                false)]));

    private static PatientInput NewInput(string name = "Jane Roe") =>
        new(0, name, new DateOnly(1990, 5, 17), "Female", "Single", "Somewhere", "0917", null);

    [Fact]
    public async Task Creating_a_patient_assigns_sequential_year_based_codes()
    {
        await using var db = _env.CreateDb();
        SignIn(ModuleAction.Create);
        var service = CreateService(db);
        var year = _env.Clock.UtcNow.ToLocalTime().Year;

        var first = await service.SaveAsync(NewInput("A"));
        var second = await service.SaveAsync(NewInput("B"));

        Assert.Equal($"{year}-00000001", first.Value.PatientCode);
        Assert.Equal($"{year}-00000002", second.Value.PatientCode);
    }

    [Fact]
    public async Task Code_sequence_continues_after_the_highest_existing_code_even_if_deleted()
    {
        await using var db = _env.CreateDb();
        var year = _env.Clock.UtcNow.ToLocalTime().Year;
        db.Patients.Add(new Patient { PatientCode = $"{year}-00000007", PatientName = "Old", IsDeleted = true });
        await db.SaveChangesAsync();
        SignIn(ModuleAction.Create);

        var created = await CreateService(db).SaveAsync(NewInput());

        Assert.Equal($"{year}-00000008", created.Value.PatientCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Name_is_required(string? name)
    {
        await using var db = _env.CreateDb();
        SignIn(ModuleAction.Create);

        var result = await CreateService(db).SaveAsync(NewInput() with { PatientName = name });

        Assert.True(result.IsFailure);
        Assert.Contains("Name", result.Error.Message, StringComparison.Ordinal);
        Assert.Empty(await db.Patients.ToListAsync());
    }

    [Fact]
    public async Task Future_birth_date_is_rejected()
    {
        await using var db = _env.CreateDb();
        SignIn(ModuleAction.Create);
        var future = DateOnly.FromDateTime(_env.Clock.UtcNow.ToLocalTime()).AddDays(2);

        var result = await CreateService(db).SaveAsync(NewInput() with { DateOfBirth = future });

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Updating_changes_the_patient_but_not_the_code()
    {
        await using var db = _env.CreateDb();
        SignIn(ModuleAction.Create, ModuleAction.Edit);
        var service = CreateService(db);
        var created = (await service.SaveAsync(NewInput())).Value;

        var updated = await service.SaveAsync(NewInput("Jane Doe") with { Id = created.Id });

        Assert.True(updated.IsSuccess);
        Assert.Equal("Jane Doe", updated.Value.PatientName);
        Assert.Equal(created.PatientCode, updated.Value.PatientCode);
    }

    [Fact]
    public async Task Updating_a_missing_patient_fails()
    {
        await using var db = _env.CreateDb();
        SignIn(ModuleAction.Edit);

        var result = await CreateService(db).SaveAsync(NewInput() with { Id = 999 });

        Assert.Equal("Patient.NotFound", result.Error.Code);
    }

    [Fact]
    public async Task A_typed_gender_or_civil_status_stays_on_the_record_and_is_not_added_to_the_lists()
    {
        await using var db = _env.CreateDb();
        SignIn(ModuleAction.Create);
        var service = CreateService(db);

        var saved = await service.SaveAsync(NewInput() with { Sex = "Non-binary", CivilStatus = "Widowed" });

        Assert.Equal("Non-binary", saved.Value.Sex);
        Assert.Equal("Widowed", saved.Value.CivilStatus);
        var entries = new EntryService(db, _env.Session);
        Assert.Empty(await entries.GetChoicesAsync(EntryFields.Gender));
        Assert.Empty(await entries.GetChoicesAsync(EntryFields.CivilStatus));
    }

    [Fact]
    public async Task Deleting_hides_the_patient_but_keeps_the_row()
    {
        await using var db = _env.CreateDb();
        SignIn(ModuleAction.Create, ModuleAction.Delete);
        var service = CreateService(db);
        var created = (await service.SaveAsync(NewInput())).Value;

        var deleted = await service.DeleteAsync(created.Id);

        Assert.True(deleted.IsSuccess);
        Assert.Equal("Patient.NotFound", (await service.GetAsync(created.Id)).Error.Code);
        Assert.Single(await db.Patients.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task Permissions_are_enforced_per_action()
    {
        await using var db = _env.CreateDb();
        SignIn(); // view only
        var service = CreateService(db);

        Assert.Equal(ForbiddenCode, (await service.SaveAsync(NewInput())).Error.Code);
        Assert.Equal(ForbiddenCode, (await service.SaveAsync(NewInput() with { Id = 1 })).Error.Code);
        Assert.Equal(ForbiddenCode, (await service.DeleteAsync(1)).Error.Code);
        Assert.True((await service.SearchAsync(new CrudSearch(null))).IsSuccess);
    }

    private const string ForbiddenCode = "Auth.Forbidden";

    [Fact]
    public async Task Users_without_the_module_cannot_even_search()
    {
        await using var db = _env.CreateDb();
        _env.Session.SignIn(new AuthenticatedUser(3, "x", "X", false, false, []));

        Assert.Equal(ForbiddenCode, (await CreateService(db).SearchAsync(new CrudSearch(null))).Error.Code);
    }

    [Fact]
    public async Task Search_pages_results_ordered_by_name_and_reports_the_total()
    {
        await using var db = _env.CreateDb();
        SignIn(ModuleAction.Create);
        var service = CreateService(db);
        foreach (var name in new[] { "Carl", "Anna", "Dina", "Bert", "Eve" })
            await service.SaveAsync(NewInput(name));

        var page1 = (await service.SearchAsync(new CrudSearch(null, 1, 2))).Value;
        var page3 = (await service.SearchAsync(new CrudSearch(null, 3, 2))).Value;

        Assert.Equal(5, page1.TotalCount);
        Assert.Equal(3, page1.TotalPages);
        Assert.Equal(["Anna", "Bert"], page1.Items.Select(i => i.PatientName));
        Assert.Equal(["Eve"], page3.Items.Select(i => i.PatientName));
    }

    [Fact]
    public async Task Search_requires_every_word_to_match_name_or_code()
    {
        await using var db = _env.CreateDb();
        SignIn(ModuleAction.Create);
        var service = CreateService(db);
        await service.SaveAsync(NewInput("Maria Clara"));
        await service.SaveAsync(NewInput("Maria Santos"));
        await service.SaveAsync(NewInput("Jose Rizal"));

        Assert.Equal(2, (await service.SearchAsync(new CrudSearch("Maria"))).Value.TotalCount);
        Assert.Equal(1, (await service.SearchAsync(new CrudSearch("Maria Santos"))).Value.TotalCount);
        Assert.Equal(1, (await service.SearchAsync(new CrudSearch("00000003"))).Value.TotalCount);
        Assert.Equal(0, (await service.SearchAsync(new CrudSearch("nobody"))).Value.TotalCount);
    }

    [Fact]
    public async Task Search_results_include_the_computed_age_and_hide_deleted_patients()
    {
        await using var db = _env.CreateDb();
        SignIn(ModuleAction.Create, ModuleAction.Delete);
        var service = CreateService(db);
        var today = DateOnly.FromDateTime(_env.Clock.UtcNow.ToLocalTime());
        var kept = (await service.SaveAsync(NewInput("Kept") with { DateOfBirth = today.AddYears(-30) })).Value;
        var gone = (await service.SaveAsync(NewInput("Gone"))).Value;
        await service.DeleteAsync(gone.Id);

        var result = (await service.SearchAsync(new CrudSearch(null))).Value;

        var item = Assert.Single(result.Items);
        Assert.Equal(kept.Id, item.Id);
        Assert.Equal("30 years old", item.Age);
    }

    [Fact]
    public void Validate_accepts_a_minimal_valid_input()
    {
        Assert.Empty(PatientService.Validate(NewInput(), new DateOnly(2026, 1, 1)));
    }
}
