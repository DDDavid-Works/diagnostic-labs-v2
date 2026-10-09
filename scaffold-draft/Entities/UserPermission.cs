using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Infrastructure.Persistence.Scaffolded.Entities;

public partial class UserPermission
{
    [Key]
    public long Id { get; set; }

    public long UserId { get; set; }

    public int ModuleId { get; set; }

    public bool ViewOnly { get; set; }

    public bool AllowCreate { get; set; }

    public bool AllowEdit { get; set; }

    public bool AllowDelete { get; set; }

    public bool AllowPrint { get; set; }

    public long CreatedByUserId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime CreatedDate { get; set; }

    public long UpdatedByUserId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime UpdatedDate { get; set; }

    [ForeignKey("ModuleId")]
    [InverseProperty("UserPermissions")]
    public virtual Module Module { get; set; } = null!;

    [ForeignKey("UserId")]
    [InverseProperty("UserPermissions")]
    public virtual User User { get; set; } = null!;
}
