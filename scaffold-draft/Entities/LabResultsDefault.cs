using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Infrastructure.Persistence.Scaffolded.Entities;

public partial class LabResultsDefault
{
    [Key]
    public long Id { get; set; }

    public int? ModuleId { get; set; }

    [Unicode(false)]
    public string Defaults { get; set; } = null!;

    public bool IsActive { get; set; }

    public long CreatedByUserId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime CreatedDate { get; set; }

    public long UpdatedByUserId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime UpdatedDate { get; set; }

    [ForeignKey("ModuleId")]
    [InverseProperty("LabResultsDefaults")]
    public virtual Module? Module { get; set; }
}
