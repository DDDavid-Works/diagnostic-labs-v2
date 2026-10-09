using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Infrastructure.Persistence.Scaffolded.Entities;

public partial class ClinicalChemistry
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

    [Column("FBSNValue")]
    [StringLength(100)]
    public string? Fbsnvalue { get; set; }

    [Column("FBSResult")]
    [StringLength(100)]
    public string? Fbsresult { get; set; }

    [Column("TotalCholesterolNValue")]
    [StringLength(100)]
    public string? TotalCholesterolNvalue { get; set; }

    [StringLength(100)]
    public string? TotalCholesterolResult { get; set; }

    [Column("TriglyceridesNValue")]
    [StringLength(100)]
    public string? TriglyceridesNvalue { get; set; }

    [StringLength(100)]
    public string? TriglyceridesResult { get; set; }

    [Column("HDLNValue")]
    [StringLength(100)]
    public string? Hdlnvalue { get; set; }

    [Column("HDLResult")]
    [StringLength(100)]
    public string? Hdlresult { get; set; }

    [Column("BUNNValue")]
    [StringLength(100)]
    public string? Bunnvalue { get; set; }

    [Column("BUNResult")]
    [StringLength(100)]
    public string? Bunresult { get; set; }

    [Column("CreatinineNValue")]
    [StringLength(100)]
    public string? CreatinineNvalue { get; set; }

    [StringLength(100)]
    public string? CreatinineResult { get; set; }

    [Column("BloodUricAcidNValue")]
    [StringLength(100)]
    public string? BloodUricAcidNvalue { get; set; }

    [StringLength(100)]
    public string? BloodUricAcidResult { get; set; }

    [Column("LDLNValue")]
    [StringLength(100)]
    public string? Ldlnvalue { get; set; }

    [Column("LDLResult")]
    [StringLength(100)]
    public string? Ldlresult { get; set; }

    [Column("SGPTNValue")]
    [StringLength(100)]
    public string? Sgptnvalue { get; set; }

    [Column("SGPTResult")]
    [StringLength(100)]
    public string? Sgptresult { get; set; }

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
