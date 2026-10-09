using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Infrastructure.Persistence.Scaffolded.Entities;

public partial class DiscountDetail
{
    [Key]
    public long Id { get; set; }

    public long DiscountId { get; set; }

    [Column(TypeName = "decimal(18, 4)")]
    public decimal? Amount { get; set; }

    [Column(TypeName = "decimal(18, 4)")]
    public decimal? Percentage { get; set; }

    public bool IsActive { get; set; }

    public long CreatedByUserId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime CreatedDate { get; set; }

    public long UpdatedByUserId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime UpdatedDate { get; set; }

    [ForeignKey("DiscountId")]
    [InverseProperty("DiscountDetails")]
    public virtual Discount Discount { get; set; } = null!;
}
