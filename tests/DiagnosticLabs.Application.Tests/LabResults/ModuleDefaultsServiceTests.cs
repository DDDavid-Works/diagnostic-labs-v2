using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.LabResults;
using DiagnosticLabs.Domain.Identity;
using DiagnosticLabs.Domain.Settings;
using DiagnosticLabs.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Application.Tests.LabResults;

public class ModuleDefaultsServiceTests
{
    private readonly TestEnvironment _env = new();

    private ModuleDefaultsService CreateService(AppDbContext db) => new(db, _env.Session);

    private void SignInAsAdmin() => _env.Session.SignIn(new AuthenticatedUser(1, "admin", "Admin", true, false, []));

    private void SignInAsClerk(bool withAccess = true) => _env.Session.SignIn(new AuthenticatedUser(
        2, "clerk", "Clerk", false, false, withAccess ? [new ModulePermission(ModuleIds.StoolFecalysis, true, true, true, true)] : []));

    private static async Task SeedModuleAsync(AppDbContext db)
    {
        db.Modules.Add(new Module { Id = ModuleIds.StoolFecalysis, ModuleName = "Stool/Fecalysis", IsActive = true });
        await db.SaveChangesAsync();
    }

    private static Dictionary<string, string?> Values() => new()
    {
        ["Color"] = " BROWN ",
        ["Result"] = "NORMAL",
        ["Remarks"] = "  ",
        ["Pathologist"] = null,
    };

    [Fact]
    public async Task Saved_defaults_come_back_trimmed_and_without_blank_values()
    {
        await using var db = _env.CreateDb();
        await SeedModuleAsync(db);
        SignInAsAdmin();
        var service = CreateService(db);

        var saved = await service.SaveAsync(ModuleIds.StoolFecalysis, Values());
        var loaded = (await service.GetAsync(ModuleIds.StoolFecalysis)).Value;

        Assert.True(saved.IsSuccess);
        Assert.Equal("BROWN", loaded["Color"]);
        Assert.Equal("NORMAL", loaded["Result"]);
        Assert.False(loaded.ContainsKey("Remarks"));
        Assert.False(loaded.ContainsKey("Pathologist"));
    }

    [Fact]
    public async Task Saving_again_replaces_the_old_defaults_and_keeps_a_single_row()
    {
        await using var db = _env.CreateDb();
        await SeedModuleAsync(db);
        SignInAsAdmin();
        var service = CreateService(db);
        await service.SaveAsync(ModuleIds.StoolFecalysis, Values());

        await service.SaveAsync(ModuleIds.StoolFecalysis, new Dictionary<string, string?> { ["Color"] = "YELLOW" });
        var loaded = (await service.GetAsync(ModuleIds.StoolFecalysis)).Value;

        Assert.Equal("YELLOW", Assert.Single(loaded).Value);
        Assert.Equal(1, await db.ModuleDefaults.CountAsync());
    }

    [Fact]
    public async Task Clearing_removes_the_defaults()
    {
        await using var db = _env.CreateDb();
        await SeedModuleAsync(db);
        SignInAsAdmin();
        var service = CreateService(db);
        await service.SaveAsync(ModuleIds.StoolFecalysis, Values());

        var cleared = await service.ClearAsync(ModuleIds.StoolFecalysis);

        Assert.True(cleared.IsSuccess);
        Assert.Empty((await service.GetAsync(ModuleIds.StoolFecalysis)).Value);
        Assert.True((await service.ClearAsync(ModuleIds.StoolFecalysis)).IsSuccess); // nothing to clear is fine
    }

    [Fact]
    public async Task Only_administrators_can_set_or_clear_but_anyone_with_the_screen_can_read()
    {
        await using var db = _env.CreateDb();
        await SeedModuleAsync(db);
        SignInAsAdmin();
        var service = CreateService(db);
        await service.SaveAsync(ModuleIds.StoolFecalysis, Values());

        SignInAsClerk();
        Assert.Equal("Auth.Forbidden", (await service.SaveAsync(ModuleIds.StoolFecalysis, Values())).Error.Code);
        Assert.Equal("Auth.Forbidden", (await service.ClearAsync(ModuleIds.StoolFecalysis)).Error.Code);
        Assert.Equal("NORMAL", (await service.GetAsync(ModuleIds.StoolFecalysis)).Value["Result"]);

        SignInAsClerk(withAccess: false);
        Assert.Equal("Auth.Forbidden", (await service.GetAsync(ModuleIds.StoolFecalysis)).Error.Code);
    }

    [Fact]
    public async Task Bad_input_is_refused()
    {
        await using var db = _env.CreateDb();
        await SeedModuleAsync(db);
        SignInAsAdmin();
        var service = CreateService(db);

        var tooLong = await service.SaveAsync(ModuleIds.StoolFecalysis, new Dictionary<string, string?> { ["Result"] = new string('x', 1001) });
        var noName = await service.SaveAsync(ModuleIds.StoolFecalysis, new Dictionary<string, string?> { [" "] = "x" });
        var noModule = await service.SaveAsync(999, Values());

        Assert.True(tooLong.IsFailure);
        Assert.True(noName.IsFailure);
        Assert.Equal("Module.NotFound", noModule.Error.Code);
        Assert.Empty(await db.ModuleDefaults.ToListAsync());
    }

    [Fact]
    public async Task A_damaged_entry_means_no_defaults_instead_of_an_error()
    {
        await using var db = _env.CreateDb();
        await SeedModuleAsync(db);
        db.ModuleDefaults.Add(new ModuleDefault { ModuleId = ModuleIds.StoolFecalysis, Defaults = "{ not json" });
        await db.SaveChangesAsync();
        SignInAsAdmin();

        var loaded = await CreateService(db).GetAsync(ModuleIds.StoolFecalysis);

        Assert.True(loaded.IsSuccess);
        Assert.Empty(loaded.Value);
    }

    [Fact]
    public async Task Defaults_of_different_screens_are_kept_apart()
    {
        await using var db = _env.CreateDb();
        await SeedModuleAsync(db);
        db.Modules.Add(new Module { Id = ModuleIds.Urinalysis, ModuleName = "Urinalysis", IsActive = true });
        await db.SaveChangesAsync();
        SignInAsAdmin();
        var service = CreateService(db);

        await service.SaveAsync(ModuleIds.StoolFecalysis, new Dictionary<string, string?> { ["Color"] = "BROWN" });
        await service.SaveAsync(ModuleIds.Urinalysis, new Dictionary<string, string?> { ["Color"] = "YELLOW" });

        Assert.Equal("BROWN", (await service.GetAsync(ModuleIds.StoolFecalysis)).Value["Color"]);
        Assert.Equal("YELLOW", (await service.GetAsync(ModuleIds.Urinalysis)).Value["Color"]);
    }
}