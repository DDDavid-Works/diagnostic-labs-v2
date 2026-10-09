using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Infrastructure.Persistence.Scaffolded.Entities;

public partial class Hematology
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

    [Column("HematocritNValue")]
    [StringLength(100)]
    public string? HematocritNvalue { get; set; }

    [StringLength(100)]
    public string? HematocritResult { get; set; }

    [Column("HemoglobinNValue")]
    [StringLength(100)]
    public string? HemoglobinNvalue { get; set; }

    [StringLength(100)]
    public string? HemoglobinResult { get; set; }

    [Column("WBCCountNValue")]
    [StringLength(100)]
    public string? WbccountNvalue { get; set; }

    [Column("WBCCountResult")]
    [StringLength(100)]
    public string? WbccountResult { get; set; }

    [Column("SegmentersNValue")]
    [StringLength(100)]
    public string? SegmentersNvalue { get; set; }

    [StringLength(100)]
    public string? SegmentersResult { get; set; }

    [Column("LymphocytesNValue")]
    [StringLength(100)]
    public string? LymphocytesNvalue { get; set; }

    [StringLength(100)]
    public string? LymphocytesResult { get; set; }

    [Column("EosinophilsNValue")]
    [StringLength(100)]
    public string? EosinophilsNvalue { get; set; }

    [StringLength(100)]
    public string? EosinophilsResult { get; set; }

    [Column("MonocytesNValue")]
    [StringLength(100)]
    public string? MonocytesNvalue { get; set; }

    [StringLength(100)]
    public string? MonocytesResult { get; set; }

    [Column("BasophilsNValue")]
    [StringLength(100)]
    public string? BasophilsNvalue { get; set; }

    [StringLength(100)]
    public string? BasophilsResult { get; set; }

    [Column("StabNValue")]
    [StringLength(100)]
    public string? StabNvalue { get; set; }

    [StringLength(100)]
    public string? StabResult { get; set; }

    [Column("PlateletCountNValue")]
    [StringLength(100)]
    public string? PlateletCountNvalue { get; set; }

    [StringLength(100)]
    public string? PlateletCountResult { get; set; }

    [StringLength(100)]
    public string? Remarks { get; set; }

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
