using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Infrastructure.Persistence.Scaffolded.Entities;

public partial class ServiceItemQuantity
{
    [Key]
    public long Id { get; set; }

    public long ServiceId { get; set; }

    public long ItemId { get; set; }

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
    [InverseProperty("ServiceItemQuantities")]
    public virtual Item Item { get; set; } = null!;

    [ForeignKey("ServiceId")]
    [InverseProperty("ServiceItemQuantities")]
    public virtual Service Service { get; set; } = null!;
}
