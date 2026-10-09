using DiagnosticLabs.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Application.Menu;

public sealed record MenuItem(int ModuleId, string Name);

public sealed record MenuGroup(string Name, IReadOnlyList<MenuItem> Items);

public interface IMenuService
{
    /// <summary>The navigation menu for the signed-in user: only active modules they may open.</summary>
    Task<IReadOnlyList<MenuGroup>> GetMenuAsync(CancellationToken cancellationToken = default);
}

public sealed class MenuService(IAppDbContext db, ICurrentUser currentUser) : IMenuService
{
    public async Task<IReadOnlyList<MenuGroup>> GetMenuAsync(CancellationToken cancellationToken = default)
    {
        var modules = await db.Modules
            .AsNoTracking()
            .Where(m => m.IsActive && m.ModuleType.IsActive && m.Id != ModuleIds.ChangePassword)
            .OrderBy(m => m.ModuleType.SortOrder).ThenBy(m => m.SortOrder)
            .Select(m => new { m.Id, m.ModuleName, GroupId = m.ModuleType.Id, Group = m.ModuleType.ModuleTypeName, m.ModuleType.IsAdmin })
            .ToListAsync(cancellationToken);

        return modules
            .Where(m => currentUser.CanAccess(m.Id) && (!m.IsAdmin || currentUser.IsAdmin))
            .GroupBy(m => (m.GroupId, m.Group))
            .Select(g => new MenuGroup(g.Key.Group, [.. g.Select(m => new MenuItem(m.Id, m.ModuleName))]))
            .ToList();
    }
}
