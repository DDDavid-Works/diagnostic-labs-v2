using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Infrastructure.Persistence.Scaffolded.Entities;

[Table("APEs")]
public partial class Ape
{
    [Key]
    public long Id { get; set; }

    public long? PatientId { get; set; }

    public long? PatientRegistrationId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? DateInputted { get; set; }

    [StringLength(200)]
    public string PatientName { get; set; } = null!;

    [StringLength(200)]
    public string? CompanyName { get; set; }

    [StringLength(100)]
    public string? DepartmentOrAgency { get; set; }

    [StringLength(50)]
    public string? Age { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? BirthDate { get; set; }

    [StringLength(10)]
    public string? Gender { get; set; }

    [StringLength(50)]
    public string? CivilStatus { get; set; }

    [StringLength(100)]
    public string? ContactNo { get; set; }

    [Column("ENT")]
    [StringLength(200)]
    public string? Ent { get; set; }

    [StringLength(200)]
    public string? Gastroenterology { get; set; }

    [StringLength(200)]
    public string? Respiratory { get; set; }

    [StringLength(200)]
    public string? IntegumentarySkin { get; set; }

    [StringLength(200)]
    public string? Cardiology { get; set; }

    [StringLength(200)]
    public string? Psychology { get; set; }

    [StringLength(200)]
    public string? Endocrinology { get; set; }

    [Column("OBGyneUrology")]
    [StringLength(200)]
    public string? ObgyneUrology { get; set; }

    [StringLength(200)]
    public string? Muscoloskeletal { get; set; }

    [StringLength(200)]
    public string? InfectiousCommunicable { get; set; }

    [StringLength(200)]
    public string? Neurological { get; set; }

    [StringLength(200)]
    public string? Surgical { get; set; }

    [StringLength(200)]
    public string? OthersPast { get; set; }

    [StringLength(200)]
    public string? Medications { get; set; }

    [StringLength(200)]
    public string? ReviewOfSystems { get; set; }

    [StringLength(200)]
    public string? Allergies { get; set; }

    public bool? IsSmoking { get; set; }

    [StringLength(200)]
    public string? SmokingSinceWhen { get; set; }

    public int? NumberOfSticksPerDay { get; set; }

    public bool? IsDrinking { get; set; }

    [StringLength(200)]
    public string? DrinkingSinceWhen { get; set; }

    public int? NumberOfBottles { get; set; }

    [StringLength(20)]
    public string? DrinkingFrequency { get; set; }

    [Column("LMP")]
    [StringLength(50)]
    public string? Lmp { get; set; }

    [Column("LMPType")]
    [StringLength(50)]
    public string Lmptype { get; set; } = null!;

    [Column("BP1st")]
    [StringLength(20)]
    public string? Bp1st { get; set; }

    [Column("BP2nd")]
    [StringLength(20)]
    public string? Bp2nd { get; set; }

    [StringLength(20)]
    public string? CardiacRate1st { get; set; }

    [StringLength(20)]
    public string? CardiacRate2nd { get; set; }

    [StringLength(20)]
    public string? Height { get; set; }

    [StringLength(20)]
    public string? Weight { get; set; }

    [Column("BMICategory")]
    [StringLength(50)]
    public string? Bmicategory { get; set; }

    [Column("VARightEyeWGlasses")]
    [StringLength(50)]
    public string? VarightEyeWglasses { get; set; }

    [Column("VARightEyeWOGlasses")]
    [StringLength(50)]
    public string? VarightEyeWoglasses { get; set; }

    [Column("VALeftEyeWGlasses")]
    [StringLength(50)]
    public string? ValeftEyeWglasses { get; set; }

    [Column("VALeftEyeWOGlasses")]
    [StringLength(50)]
    public string? ValeftEyeWoglasses { get; set; }

    [StringLength(20)]
    public string? VisualAcuity { get; set; }

    [StringLength(2)]
    public string? Skin { get; set; }

    [StringLength(2)]
    public string? HeadScalp { get; set; }

    [StringLength(2)]
    public string? Eyes { get; set; }

    [StringLength(2)]
    public string? Ears { get; set; }

    [StringLength(2)]
    public string? Nose { get; set; }

    [StringLength(2)]
    public string? TeethTonsilsThroatPharynx { get; set; }

    [StringLength(2)]
    public string? NeckLymphNodesThyroid { get; set; }

    [StringLength(2)]
    public string? ThoraxBreast { get; set; }

    [StringLength(2)]
    public string? HeartLungs { get; set; }

    [StringLength(2)]
    public string? AbdomenLiverSpleen { get; set; }

    [StringLength(2)]
    public string? InguinalAreaGenitalsAnus { get; set; }

    [StringLength(2)]
    public string? ExtremetiesSpine { get; set; }

    [StringLength(2)]
    public string? Tattoo { get; set; }

    [StringLength(2)]
    public string? MassCyst { get; set; }

    [Column("OthersPE")]
    [StringLength(2)]
    public string? OthersPe { get; set; }

    [StringLength(500)]
    public string? Findings { get; set; }

    [StringLength(50)]
    public string VitalSignsBy { get; set; } = null!;

    [StringLength(50)]
    public string HeightWeightBy { get; set; } = null!;

    public bool IsActive { get; set; }

    public long CreatedByUserId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime CreatedDate { get; set; }

    public long UpdatedByUserId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime UpdatedDate { get; set; }
}
