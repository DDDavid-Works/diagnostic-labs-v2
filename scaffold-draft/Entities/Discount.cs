using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Infrastructure.Persistence.Scaffolded.Entities;

public partial class Discount
{
    [Key]
    public long Id { get; set; }

    [StringLength(50)]
    public string DiscountName { get; set; } = null!;

    [StringLength(200)]
    public string DiscountDescription { get; set; } = null!;

    public bool IsActive { get; set; }

    public long CreatedByUserId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime CreatedDate { get; set; }

    public long UpdatedByUserId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime UpdatedDate { get; set; }

    [InverseProperty("Discount")]
    public virtual ICollection<DiscountDetail> DiscountDetails { get; set; } = new List<DiscountDetail>();
}
