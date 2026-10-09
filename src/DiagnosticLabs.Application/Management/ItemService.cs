using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Domain.Inventory;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Application.Management;

/// <summary>Stock of an item at one location. <c>Id = 0</c> is a new row.</summary>
public sealed record ItemQuantityLine(long Id, long ItemLocationId, decimal Quantity);

public sealed record ItemListItem(long Id, string ItemName, decimal Cost, decimal TotalQuantity, bool IsActive) : IHasId;

public sealed record ItemDetails(
    long Id, string ItemName, decimal Cost, bool IsActive, IReadOnlyList<ItemQuantityLine> Quantities, byte[] RowVersion) : IHasId;

public sealed record ItemInput(
    long Id, string? ItemName, decimal Cost, bool IsActive, IReadOnlyList<ItemQuantityLine> Quantities, byte[]? RowVersion) : IReferenceInput;

public interface IItemService : ICrudService<ItemListItem, ItemDetails, ItemInput>;

public sealed class ItemService(IAppDbContext db, ICurrentUser user)
    : ReferenceCrudService<Item, ItemListItem, ItemDetails, ItemInput>(db, user, ModuleIds.Items, "Item"),
      IItemService
{
    protected override DbSet<Item> Set => Db.Items;

    protected override IQueryable<Item> ForListing(IQueryable<Item> q) => q.Include(i => i.Quantities);

    protected override IQueryable<Item> ForEditing(IQueryable<Item> q) => q.Include(i => i.Quantities);

    protected override IQueryable<Item> Matches(IQueryable<Item> q, string word) => q.Where(i => i.ItemName.Contains(word));

    protected override IOrderedQueryable<Item> Order(IQueryable<Item> q) => q.OrderBy(i => i.ItemName).ThenBy(i => i.Id);

    protected override ItemListItem ToListItem(Item i) =>
        new(i.Id, i.ItemName, i.Cost, i.Quantities.Sum(q => q.Quantity), i.IsActive);

    protected override ItemDetails ToDetails(Item i) =>
        new(i.Id, i.ItemName, i.Cost, i.IsActive,
            [.. i.Quantities.Where(q => !q.IsDeleted).OrderBy(q => q.ItemLocationId).Select(q => new ItemQuantityLine(q.Id, q.ItemLocationId, q.Quantity))],
            i.RowVersion);

    protected override IReadOnlyList<string> Validate(ItemInput input)
    {
        var errors = new List<string>();
        Rules.Required(errors, input.ItemName, "Name", 50);
        Rules.NotNegative(errors, input.Cost, "Cost");

        if (input.Quantities.Any(q => q.Quantity < 0))
            errors.Add("Quantities must not be negative.");
        if (input.Quantities.GroupBy(q => q.ItemLocationId).Any(g => g.Count() > 1))
            errors.Add("An item can only be listed once per location.");

        return errors;
    }

    protected override async Task<IReadOnlyList<string>> ValidateAsync(ItemInput input, CancellationToken cancellationToken)
    {
        var ids = input.Quantities.Select(q => q.ItemLocationId).Distinct().ToList();
        var found = await Db.ItemLocations.CountAsync(l => ids.Contains(l.Id), cancellationToken);
        return found == ids.Count ? [] : ["One or more item locations no longer exist."];
    }

    protected override void ApplyFields(Item item, ItemInput input)
    {
        item.ItemName = input.ItemName!.Trim();
        item.Cost = input.Cost;

        SyncChildren(
            item.Quantities,
            input.Quantities,
            line => line.Id,
            child => child.Id,
            (child, line) => child.ItemLocationId == line.ItemLocationId,
            (child, line) =>
            {
                child.ItemLocationId = line.ItemLocationId;
                child.Quantity = line.Quantity;
            },
            child => Db.ItemQuantities.Remove(child));
    }
}
