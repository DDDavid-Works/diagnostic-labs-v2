using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Infrastructure.Persistence.Scaffolded.Entities;

public partial class Package
{
    [Key]
    public long Id { get; set; }

    [StringLength(50)]
    public string PackageName { get; set; } = null!;

    [StringLength(200)]
    public string PackageDescription { get; set; } = null!;

    [Column(TypeName = "decimal(18, 4)")]
    public decimal Price { get; set; }

    public long? CompanyId { get; set; }

    public bool IsActive { get; set; }

    public long CreatedByUserId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime CreatedDate { get; set; }

    public long UpdatedByUserId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime UpdatedDate { get; set; }

    [ForeignKey("CompanyId")]
    [InverseProperty("Packages")]
    public virtual Company? Company { get; set; }

    [InverseProperty("Package")]
    public virtual ICollection<PackageService> PackageServices { get; set; } = new List<PackageService>();

    [InverseProperty("Package")]
    public virtual ICollection<PatientRegistration> PatientRegistrations { get; set; } = new List<PatientRegistration>();
}
