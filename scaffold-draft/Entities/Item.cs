using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Infrastructure.Persistence.Scaffolded.Entities;

public partial class Item
{
    [Key]
    public long Id { get; set; }

    [StringLength(50)]
    public string ItemName { get; set; } = null!;

    [Column(TypeName = "decimal(18, 4)")]
    public decimal Cost { get; set; }

    public bool IsActive { get; set; }

    public long CreatedByUserId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime CreatedDate { get; set; }

    public long UpdatedByUserId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime UpdatedDate { get; set; }

    [InverseProperty("Item")]
    public virtual ICollection<ItemQuantity> ItemQuantities { get; set; } = new List<ItemQuantity>();

    [InverseProperty("Item")]
    public virtual ICollection<ServiceItemQuantity> ServiceItemQuantities { get; set; } = new List<ServiceItemQuantity>();
}
