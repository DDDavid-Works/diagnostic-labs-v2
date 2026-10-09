using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Infrastructure.Persistence.Scaffolded.Entities;

public partial class Urinalyse
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

    [StringLength(50)]
    public string? Color { get; set; }

    [StringLength(50)]
    public string? Appearance { get; set; }

    [StringLength(50)]
    public string? Reaction { get; set; }

    [Column("SPGravity")]
    [StringLength(50)]
    public string? Spgravity { get; set; }

    [StringLength(50)]
    public string? Albumin { get; set; }

    [StringLength(50)]
    public string? Sugar { get; set; }

    [StringLength(50)]
    public string? PusCells { get; set; }

    [StringLength(50)]
    public string? RedCells { get; set; }

    [StringLength(50)]
    public string? MucusThreads { get; set; }

    [StringLength(50)]
    public string? EpithelialCells { get; set; }

    [Column("AmorphousUratesPO4")]
    [StringLength(50)]
    public string? AmorphousUratesPo4 { get; set; }

    [StringLength(50)]
    public string? Bacteria { get; set; }

    [StringLength(50)]
    public string? Casts { get; set; }

    [StringLength(50)]
    public string? Crystals { get; set; }

    [StringLength(500)]
    public string Others { get; set; } = null!;

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
