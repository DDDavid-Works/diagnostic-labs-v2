using DiagnosticLabs.Domain.Catalog;
using DiagnosticLabs.Domain.Common;

namespace DiagnosticLabs.Domain.Inventory;

public class Item : ReferenceEntity
{
    public string ItemName { get; set; } = string.Empty;

    public decimal Cost { get; set; }

    public ICollection<ItemQuantity> Quantities { get; set; } = [];
}

public class ItemLocation : ReferenceEntity
{
    public string ItemLocationName { get; set; } = string.Empty;
}

public class ItemQuantity : SoftDeletableEntity
{
    public long ItemId { get; set; }

    public Item Item { get; set; } = null!;

    public long ItemLocationId { get; set; }

    public ItemLocation ItemLocation { get; set; } = null!;

    public decimal Quantity { get; set; }
}

/// <summary>How much of an inventory item one performance of a service consumes.</summary>
public class ServiceItemQuantity : SoftDeletableEntity
{
    public long ServiceId { get; set; }

    public Service Service { get; set; } = null!;

    public long ItemId { get; set; }

    public Item Item { get; set; } = null!;

    public decimal Quantity { get; set; }
}
