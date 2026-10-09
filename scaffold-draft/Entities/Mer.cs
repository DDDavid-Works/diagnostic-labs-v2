using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Infrastructure.Persistence.Scaffolded.Entities;

[Table("MERs")]
public partial class Mer
{
    [Key]
    public long Id { get; set; }

    public long? PatientId { get; set; }

    public long? PatientRegistrationId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? DateInputted { get; set; }

    [StringLength(200)]
    public string PatientName { get; set; } = null!;

    [StringLength(100)]
    public string? ContactNo { get; set; }

    [StringLength(50)]
    public string? Age { get; set; }

    [StringLength(10)]
    public string? Gender { get; set; }

    [StringLength(50)]
    public string? CivilStatus { get; set; }

    [StringLength(200)]
    public string CompanyName { get; set; } = null!;

    [StringLength(10)]
    public string? ChestXray { get; set; }

    [StringLength(100)]
    public string? ChestXrayRemarks { get; set; }

    [Column("CBC")]
    [StringLength(10)]
    public string? Cbc { get; set; }

    [Column("CBCRemarks")]
    [StringLength(100)]
    public string? Cbcremarks { get; set; }

    [StringLength(10)]
    public string? Urinalysis { get; set; }

    [StringLength(100)]
    public string? UrinalysisRemarks { get; set; }

    [StringLength(10)]
    public string? Fecalysis { get; set; }

    [StringLength(100)]
    public string? FecalysisRemarks { get; set; }

    [Column("HBsAg")]
    [StringLength(10)]
    public string? HbsAg { get; set; }

    [Column("HBsAgRemarks")]
    [StringLength(100)]
    public string? HbsAgRemarks { get; set; }

    [StringLength(10)]
    public string? DrugTest2Panel { get; set; }

    [StringLength(100)]
    public string? DrugTest2PanelRemarks { get; set; }

    [StringLength(10)]
    public string? DrugTest4Panel { get; set; }

    [StringLength(100)]
    public string? DrugTest4PanelRemarks { get; set; }

    [StringLength(10)]
    public string? Classification { get; set; }

    [StringLength(500)]
    public string? MedicalSurgicalHistory { get; set; }

    [StringLength(500)]
    public string? Assessment { get; set; }

    [StringLength(500)]
    public string? Remarks { get; set; }

    [StringLength(250)]
    public string? AssessmentDoneBy { get; set; }

    [StringLength(250)]
    public string? PhysicianName { get; set; }

    [StringLength(250)]
    public string? PhysicianLicense { get; set; }

    public bool IsActive { get; set; }

    public long CreatedByUserId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime CreatedDate { get; set; }

    public long UpdatedByUserId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime UpdatedDate { get; set; }
}
