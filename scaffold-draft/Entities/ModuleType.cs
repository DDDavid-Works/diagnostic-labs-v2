using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Infrastructure.Persistence.Scaffolded.Entities;

public partial class ModuleType
{
    [Key]
    public int Id { get; set; }

    [StringLength(50)]
    public string ModuleTypeName { get; set; } = null!;

    [StringLength(50)]
    public string Icon { get; set; } = null!;

    public int SortOrder { get; set; }

    public bool IsActive { get; set; }

    public bool IsAdmin { get; set; }

    [InverseProperty("ModuleType")]
    public virtual ICollection<Module> Modules { get; set; } = new List<Module>();
}
