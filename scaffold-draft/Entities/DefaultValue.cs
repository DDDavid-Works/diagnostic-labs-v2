using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Infrastructure.Persistence.Scaffolded.Entities;

public partial class DefaultValue
{
    [Key]
    public long Id { get; set; }

    public int ModuleId { get; set; }

    [StringLength(50)]
    [Unicode(false)]
    public string FieldName { get; set; } = null!;

    [StringLength(100)]
    [Unicode(false)]
    public string FieldValueTitle { get; set; } = null!;

    [StringLength(200)]
    [Unicode(false)]
    public string FieldValue { get; set; } = null!;

    public bool IsActive { get; set; }

    public long CreatedByUserId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime CreatedDate { get; set; }

    public long UpdatedByUserId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime UpdatedDate { get; set; }
}
