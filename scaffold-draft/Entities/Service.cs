using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Infrastructure.Persistence.Scaffolded.Entities;

public partial class Service
{
    [Key]
    public long Id { get; set; }

    [StringLength(50)]
    public string ServiceName { get; set; } = null!;

    [StringLength(200)]
    public string ServiceDescription { get; set; } = null!;

    [Column(TypeName = "decimal(18, 4)")]
    public decimal Price { get; set; }

    public bool IsActive { get; set; }

    public long CreatedByUserId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime CreatedDate { get; set; }

    public long UpdatedByUserId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime UpdatedDate { get; set; }

    [InverseProperty("Service")]
    public virtual ICollection<Module> Modules { get; set; } = new List<Module>();

    [InverseProperty("Service")]
    public virtual ICollection<PackageService> PackageServices { get; set; } = new List<PackageService>();

    [InverseProperty("Service")]
    public virtual ICollection<PatientRegistrationService> PatientRegistrationServices { get; set; } = new List<PatientRegistrationService>();

    [InverseProperty("Service")]
    public virtual ICollection<ServiceItemQuantity> ServiceItemQuantities { get; set; } = new List<ServiceItemQuantity>();
}
