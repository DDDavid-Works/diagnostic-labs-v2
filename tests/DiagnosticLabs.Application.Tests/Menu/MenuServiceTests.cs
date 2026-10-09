using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Menu;
using DiagnosticLabs.Domain.Identity;

namespace DiagnosticLabs.Application.Tests.Menu;

public class MenuServiceTests
{
    private readonly TestEnvironment _env = new();

    private async Task<MenuService> CreateAsync(bool isAdmin, params int[] moduleIds)
    {
        var db = _env.CreateDb();
        var regular = new ModuleType { Id = 1, ModuleTypeName = "Registration", SortOrder = 1, IsActive = true };
        var settings = new ModuleType { Id = 2, ModuleTypeName = "Settings", SortOrder = 2, IsActive = true, IsAdmin = true };
        db.Modules.AddRange(
            new Module { Id = 2, ModuleName = "Patients", ModuleType = regular, SortOrder = 1, IsActive = true },
            new Module { Id = 3, ModuleName = "Companies", ModuleType = regular, SortOrder = 2, IsActive = true },
            new Module { Id = 4, ModuleName = "Retired", ModuleType = regular, SortOrder = 3, IsActive = false },
            new Module { Id = 27, ModuleName = "Users", ModuleType = settings, SortOrder = 1, IsActive = true });
        await db.SaveChangesAsync();

        _env.Session.SignIn(new AuthenticatedUser(
            1, "u", "U", isAdmin, false, [.. moduleIds.Select(id => new ModulePermission(id, false, false, false, false))]));
        return new MenuService(db, _env.Session);
    }

    [Fact]
    public async Task Users_only_see_modules_they_have_permission_for()
    {
        var menu = await (await CreateAsync(isAdmin: false, 2)).GetMenuAsync();

        var group = Assert.Single(menu);
        Assert.Equal("Registration", group.Name);
        Assert.Equal(["Patients"], group.Items.Select(i => i.Name));
    }

    [Fact]
    public async Task Admin_only_groups_are_hidden_from_non_admins_even_with_a_permission_row()
    {
        var menu = await (await CreateAsync(isAdmin: false, 2, 27)).GetMenuAsync();

        Assert.DoesNotContain(menu, g => g.Name == "Settings");
    }

    [Fact]
    public async Task Admins_see_every_active_module_in_order()
    {
        var menu = await (await CreateAsync(isAdmin: true)).GetMenuAsync();

        Assert.Equal(["Registration", "Settings"], menu.Select(g => g.Name));
        Assert.Equal(["Patients", "Companies"], menu[0].Items.Select(i => i.Name));
    }
}
