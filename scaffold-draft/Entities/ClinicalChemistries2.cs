using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Infrastructure.Persistence.Scaffolded.Entities;

[Table("ClinicalChemistries2")]
public partial class ClinicalChemistries2
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

    [Column("AlkalinePhosphataseCNValue")]
    [StringLength(100)]
    public string? AlkalinePhosphataseCnvalue { get; set; }

    [Column("AlkalinePhosphataseCUnit")]
    [StringLength(100)]
    public string? AlkalinePhosphataseCunit { get; set; }

    [Column("AlkalinePhosphataseCResults")]
    [StringLength(100)]
    public string? AlkalinePhosphataseCresults { get; set; }

    [Column("AlkalinePhosphataseSNValue")]
    [StringLength(100)]
    public string? AlkalinePhosphataseSnvalue { get; set; }

    [Column("AlkalinePhosphataseSUnit")]
    [StringLength(100)]
    public string? AlkalinePhosphataseSunit { get; set; }

    [Column("AlkalinePhosphataseSResults")]
    [StringLength(100)]
    public string? AlkalinePhosphataseSresults { get; set; }

    [Column("SGOTCNValue")]
    [StringLength(100)]
    public string? Sgotcnvalue { get; set; }

    [Column("SGOTCUnit")]
    [StringLength(100)]
    public string? Sgotcunit { get; set; }

    [Column("SGOTCResults")]
    [StringLength(100)]
    public string? Sgotcresults { get; set; }

    [Column("SGOTSNValue")]
    [StringLength(100)]
    public string? Sgotsnvalue { get; set; }

    [Column("SGOTSUnit")]
    [StringLength(100)]
    public string? Sgotsunit { get; set; }

    [Column("SGOTSResults")]
    [StringLength(100)]
    public string? Sgotsresults { get; set; }

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
