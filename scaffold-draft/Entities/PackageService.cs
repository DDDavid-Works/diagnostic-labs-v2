using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Infrastructure.Persistence.Scaffolded.Entities;

public partial class PackageService
{
    [Key]
    public long Id { get; set; }

    public long PackageId { get; set; }

    public long ServiceId { get; set; }

    [Column(TypeName = "decimal(18, 4)")]
    public decimal Price { get; set; }

    public bool IsActive { get; set; }

    public long CreatedByUserId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime CreatedDate { get; set; }

    public long UpdatedByUserId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime UpdatedDate { get; set; }

    [ForeignKey("PackageId")]
    [InverseProperty("PackageServices")]
    public virtual Package Package { get; set; } = null!;

    [ForeignKey("ServiceId")]
    [InverseProperty("PackageServices")]
    public virtual Service Service { get; set; } = null!;
}
