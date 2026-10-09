using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Infrastructure.Persistence.Scaffolded.Entities;

public partial class Module
{
    [Key]
    public int Id { get; set; }

    public int ModuleTypeId { get; set; }

    [StringLength(50)]
    public string ModuleName { get; set; } = null!;

    public bool HasView { get; set; }

    public bool HasCreate { get; set; }

    public bool HasEdit { get; set; }

    public bool HasDelete { get; set; }

    public bool HasSearch { get; set; }

    public bool HasPrint { get; set; }

    public bool HasShowList { get; set; }

    public bool HasSetDefaults { get; set; }

    [StringLength(50)]
    public string Icon { get; set; } = null!;

    public int SortOrder { get; set; }

    public bool IsActive { get; set; }

    public long? ServiceId { get; set; }

    [InverseProperty("Module")]
    public virtual ICollection<LabResultsDefault> LabResultsDefaults { get; set; } = new List<LabResultsDefault>();

    [ForeignKey("ModuleTypeId")]
    [InverseProperty("Modules")]
    public virtual ModuleType ModuleType { get; set; } = null!;

    [InverseProperty("Module")]
    public virtual ICollection<MultiLineEntry> MultiLineEntries { get; set; } = new List<MultiLineEntry>();

    [ForeignKey("ServiceId")]
    [InverseProperty("Modules")]
    public virtual Service? Service { get; set; }

    [InverseProperty("Module")]
    public virtual ICollection<SingleLineEntry> SingleLineEntries { get; set; } = new List<SingleLineEntry>();

    [InverseProperty("Module")]
    public virtual ICollection<UserPermission> UserPermissions { get; set; } = new List<UserPermission>();
}
