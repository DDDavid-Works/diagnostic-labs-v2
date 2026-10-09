using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Application.Common;

public interface IHasId
{
    long Id { get; }
}

/// <summary>What a screen submits. <c>Id = 0</c> creates; otherwise it updates, guarded by <see cref="RowVersion"/>.</summary>
public interface ICrudInput : IHasId
{
    byte[]? RowVersion { get; }
}

/// <summary>Input for reference lists, which can be switched off instead of deleted.</summary>
public interface IReferenceInput : ICrudInput
{
    bool IsActive { get; }
}

/// <summary>Plain text search with paging. Screens with extra filters derive from it (e.g. <c>RegistrationSearch</c>).</summary>
public record CrudSearch(
    string? Text,
    int Page = 1,
    int PageSize = Paging.DefaultPageSize,
    bool IncludeInactive = false);

/// <summary>The operations every maintenance screen needs; the generic list/editor view model talks to this.</summary>
public interface ICrudService<TListItem, TDetails, in TInput>
    where TListItem : IHasId
    where TDetails : IHasId
    where TInput : ICrudInput
{
    Task<Result<PagedResult<TListItem>>> SearchAsync(CrudSearch search, CancellationToken cancellationToken = default);

    Task<Result<TDetails>> GetAsync(long id, CancellationToken cancellationToken = default);

    Task<Result<TDetails>> SaveAsync(TInput input, CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(long id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Shared create / read / update / delete workflow: permission checks, validation, optimistic
/// concurrency, paging and the error shapes. A concrete service only describes its entity.
/// </summary>
public abstract class CrudService<TEntity, TListItem, TDetails, TInput>(
    IAppDbContext db,
    ICurrentUser currentUser,
    int moduleId,
    string entityName) : ICrudService<TListItem, TDetails, TInput>
    where TEntity : AuditableEntity, new()
    where TListItem : IHasId
    where TDetails : IHasId
    where TInput : ICrudInput
{
    protected IAppDbContext Db { get; } = db;

    protected ICurrentUser CurrentUser { get; } = currentUser;

    protected string EntityName { get; } = entityName;

    protected abstract DbSet<TEntity> Set { get; }

    /// <summary>Restricts a query to one search word (for example name or code contains it).</summary>
    protected abstract IQueryable<TEntity> Matches(IQueryable<TEntity> query, string word);

    protected abstract IOrderedQueryable<TEntity> Order(IQueryable<TEntity> query);

    protected abstract TListItem ToListItem(TEntity entity);

    protected abstract TDetails ToDetails(TEntity entity);

    protected abstract IReadOnlyList<string> Validate(TInput input);

    /// <summary>Copies the input onto the entity (and its child rows).</summary>
    protected abstract void Apply(TEntity entity, TInput input);

    protected abstract IQueryable<TEntity> ApplyActiveFilter(IQueryable<TEntity> query, bool includeInactive);

    /// <summary>Removes the row the way this kind of data is removed (deactivate or soft delete).</summary>
    protected abstract void Remove(TEntity entity);

    /// <summary>Query used when one row is opened or edited; add <c>Include</c>s for child rows here.</summary>
    protected virtual IQueryable<TEntity> ForEditing(IQueryable<TEntity> query) => query;

    /// <summary>Query used for the search list; include whatever <see cref="ToListItem"/> reads (e.g. a related name).</summary>
    protected virtual IQueryable<TEntity> ForListing(IQueryable<TEntity> query) => query;

    /// <summary>Extra checks that depend on the database (e.g. referenced rows exist). Runs after <see cref="Validate"/>.</summary>
    protected virtual Task<IReadOnlyList<string>> ValidateAsync(TInput input, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<string>>([]);

    protected virtual Task<TEntity> CreateAsync(TInput input, CancellationToken cancellationToken) =>
        Task.FromResult(new TEntity());

    protected virtual Task AfterSaveAsync(TEntity entity, TInput input, CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Return an error to block editing or deleting this particular row (e.g. system rows).</summary>
    protected virtual Error? CheckCanModify(TEntity entity) => null;

    /// <summary>Applies filters beyond the search text; screens with extra filters override this.</summary>
    protected virtual IQueryable<TEntity> Filter(IQueryable<TEntity> query, CrudSearch search) => query;

    /// <summary>Extra rules that only apply to removing a row (e.g. never remove the last administrator).</summary>
    protected virtual Task<Error?> CheckCanDeleteAsync(TEntity entity, CancellationToken cancellationToken) =>
        Task.FromResult<Error?>(null);

    public async Task<Result<PagedResult<TListItem>>> SearchAsync(CrudSearch search, CancellationToken cancellationToken = default)
    {
        if (!CurrentUser.Can(moduleId, ModuleAction.View))
            return Result<PagedResult<TListItem>>.Failure(Errors.Forbidden);

        var (page, pageSize) = Paging.Normalize(search.Page, search.PageSize);
        var query = Filter(ApplyActiveFilter(ForListing(Set.AsNoTracking()), search.IncludeInactive), search);

        // Every word must match, so "juan 2026" narrows the list.
        foreach (var word in (search.Text ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            query = Matches(query, word);

        var total = await query.CountAsync(cancellationToken);
        var rows = await Order(query)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = rows.Select(ToListItem).ToList();
        return Result<PagedResult<TListItem>>.Success(new PagedResult<TListItem>(items, total, page, pageSize));
    }

    public async Task<Result<TDetails>> GetAsync(long id, CancellationToken cancellationToken = default)
    {
        if (!CurrentUser.Can(moduleId, ModuleAction.View))
            return Result<TDetails>.Failure(Errors.Forbidden);

        var entity = await ForEditing(Set.AsNoTracking()).FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        return entity is null
            ? Result<TDetails>.Failure(Errors.NotFound(EntityName))
            : Result<TDetails>.Success(ToDetails(entity));
    }

    public async Task<Result<TDetails>> SaveAsync(TInput input, CancellationToken cancellationToken = default)
    {
        var isNew = input.Id == 0;
        if (!CurrentUser.Can(moduleId, isNew ? ModuleAction.Create : ModuleAction.Edit))
            return Result<TDetails>.Failure(Errors.Forbidden);

        var errors = Validate(input).ToList();
        if (errors.Count == 0)
            errors.AddRange(await ValidateAsync(input, cancellationToken));
        if (errors.Count > 0)
            return Result<TDetails>.Failure(Errors.Invalid(errors));

        TEntity entity;
        if (isNew)
        {
            entity = await CreateAsync(input, cancellationToken);
            Set.Add(entity);
        }
        else
        {
            var existing = await ForEditing(Set).FirstOrDefaultAsync(e => e.Id == input.Id, cancellationToken);
            if (existing is null)
                return Result<TDetails>.Failure(Errors.NotFound(EntityName));

            if (CheckCanModify(existing) is { } blocked)
                return Result<TDetails>.Failure(blocked);

            entity = existing;
            if (input.RowVersion is { Length: > 0 })
                Db.SetOriginalRowVersion(entity, input.RowVersion);
        }

        Apply(entity, input);

        try
        {
            await Db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<TDetails>.Failure(Errors.Conflict(EntityName));
        }

        await AfterSaveAsync(entity, input, cancellationToken);
        return Result<TDetails>.Success(ToDetails(entity));
    }

    public async Task<Result> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        if (!CurrentUser.Can(moduleId, ModuleAction.Delete))
            return Result.Failure(Errors.Forbidden);

        var entity = await Set.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (entity is null)
            return Result.Failure(Errors.NotFound(EntityName));

        if (CheckCanModify(entity) is { } blocked)
            return Result.Failure(blocked);

        if (await CheckCanDeleteAsync(entity, cancellationToken) is { } notAllowed)
            return Result.Failure(notAllowed);

        Remove(entity);
        await Db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    /// <summary>
    /// Syncs a child collection with the submitted lines: a line updates the child with the same id
    /// (or, for a new line, the same business key, so removing and re-adding a row in one save is an update),
    /// extra lines are added and children that are no longer submitted are removed.
    /// </summary>
    protected static void SyncChildren<TChild, TLine>(
        ICollection<TChild> existing,
        IEnumerable<TLine> lines,
        Func<TLine, long> lineId,
        Func<TChild, long> childId,
        Func<TChild, TLine, bool> sameKey,
        Action<TChild, TLine> copy,
        Action<TChild> remove)
        where TChild : class, new()
    {
        var unmatched = existing.ToList();

        foreach (var line in lines)
        {
            var child = lineId(line) != 0 ? unmatched.FirstOrDefault(c => childId(c) == lineId(line)) : null;
            child ??= unmatched.FirstOrDefault(c => sameKey(c, line));

            if (child is null)
            {
                child = new TChild();
                existing.Add(child);
            }
            else
            {
                unmatched.Remove(child);
            }

            copy(child, line);
        }

        foreach (var child in unmatched)
            remove(child);
    }

    protected static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>CRUD for lists whose rows are switched off (<c>IsActive = false</c>) instead of being deleted.</summary>
public abstract class ReferenceCrudService<TEntity, TListItem, TDetails, TInput>(
    IAppDbContext db,
    ICurrentUser currentUser,
    int moduleId,
    string entityName) : CrudService<TEntity, TListItem, TDetails, TInput>(db, currentUser, moduleId, entityName)
    where TEntity : ReferenceEntity, new()
    where TListItem : IHasId
    where TDetails : IHasId
    where TInput : IReferenceInput
{
    protected abstract void ApplyFields(TEntity entity, TInput input);

    protected sealed override void Apply(TEntity entity, TInput input)
    {
        entity.IsActive = input.IsActive;
        ApplyFields(entity, input);
    }

    protected sealed override IQueryable<TEntity> ApplyActiveFilter(IQueryable<TEntity> query, bool includeInactive) =>
        includeInactive ? query : query.Where(e => e.IsActive);

    protected sealed override void Remove(TEntity entity) => entity.IsActive = false;
}
