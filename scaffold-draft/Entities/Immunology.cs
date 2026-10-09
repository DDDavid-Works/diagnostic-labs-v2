using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Infrastructure.Persistence.Scaffolded.Entities;

public partial class Immunology
{
    [Key]
    public long Id { get; set; }

    public long? PatientId { get; set; }

    public long? PatientRegistrationId { get; set; }

    [StringLength(200)]
    public string PatientCode { get; set; } = null!;

    [StringLength(200)]
    public string PatientName { get; set; } = null!;

    [StringLength(200)]
    public string? CompanyOrPhysician { get; set; }

    [StringLength(50)]
    public string? Age { get; set; }

    [StringLength(20)]
    public string? Sex { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? DateRequested { get; set; }

    public byte[]? Photo { get; set; }

    [StringLength(100)]
    public string? Test { get; set; }

    [StringLength(500)]
    public string Result { get; set; } = null!;

    [StringLength(500)]
    public string Remarks { get; set; } = null!;

    [StringLength(100)]
    public string MedicalTechnologist { get; set; } = null!;

    [StringLength(100)]
    public string Pathologist { get; set; } = null!;

    public bool IsActive { get; set; }

    public long CreatedByUserId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime CreatedDate { get; set; }

    public long UpdatedByUserId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime UpdatedDate { get; set; }
}
