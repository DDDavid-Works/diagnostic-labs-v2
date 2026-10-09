using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Infrastructure.Persistence.Scaffolded.Entities;

public partial class Company
{
    [Key]
    public long Id { get; set; }

    [StringLength(200)]
    public string CompanyName { get; set; } = null!;

    [StringLength(500)]
    public string Address { get; set; } = null!;

    [StringLength(200)]
    public string ContactNumbers { get; set; } = null!;

    [StringLength(200)]
    public string ContactPerson { get; set; } = null!;

    public bool IsSystem { get; set; }

    public bool IsActive { get; set; }

    public long CreatedByUserId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime CreatedDate { get; set; }

    public long UpdatedByUserId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime UpdatedDate { get; set; }

    [InverseProperty("Company")]
    public virtual ICollection<Package> Packages { get; set; } = new List<Package>();

    [InverseProperty("Company")]
    public virtual ICollection<PatientRegistration> PatientRegistrations { get; set; } = new List<PatientRegistration>();
}
