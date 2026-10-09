using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Infrastructure.Persistence.Scaffolded.Entities;

public partial class User
{
    [Key]
    public long Id { get; set; }

    [StringLength(100)]
    public string Username { get; set; } = null!;

    [StringLength(200)]
    public string FullName { get; set; } = null!;

    [StringLength(200)]
    public string Password { get; set; } = null!;

    public bool IsActive { get; set; }

    public bool IsAdmin { get; set; }

    public long CreatedByUserId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime CreatedDate { get; set; }

    public long UpdatedByUserId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime UpdatedDate { get; set; }

    [InverseProperty("User")]
    public virtual ICollection<UserPermission> UserPermissions { get; set; } = new List<UserPermission>();
}
