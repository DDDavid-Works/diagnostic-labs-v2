using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Infrastructure.Persistence.Scaffolded.Entities;

public partial class ItemQuantity
{
    [Key]
    public long Id { get; set; }

    public long ItemId { get; set; }

    public long ItemLocationId { get; set; }

    [Column(TypeName = "decimal(18, 4)")]
    public decimal Quantity { get; set; }

    public bool IsActive { get; set; }

    public long CreatedByUserId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime CreatedDate { get; set; }

    public long UpdatedByUserId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime UpdatedDate { get; set; }

    [ForeignKey("ItemId")]
    [InverseProperty("ItemQuantities")]
    public virtual Item Item { get; set; } = null!;

    [ForeignKey("ItemLocationId")]
    [InverseProperty("ItemQuantities")]
    public virtual ItemLocation ItemLocation { get; set; } = null!;
}
