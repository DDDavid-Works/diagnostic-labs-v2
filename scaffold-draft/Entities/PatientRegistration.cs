using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Infrastructure.Persistence.Scaffolded.Entities;

public partial class PatientRegistration
{
    [Key]
    public long Id { get; set; }

    [StringLength(100)]
    public string RegistrationCode { get; set; } = null!;

    [Column(TypeName = "datetime")]
    public DateTime InputDate { get; set; }

    public long PatientId { get; set; }

    public long? CompanyId { get; set; }

    public long? PackageId { get; set; }

    [StringLength(200)]
    public string BatchName { get; set; } = null!;

    [Column(TypeName = "decimal(18, 4)")]
    public decimal AmountDue { get; set; }

    [Column(TypeName = "decimal(18, 4)")]
    public decimal? DiscountAmount { get; set; }

    [Column(TypeName = "decimal(18, 4)")]
    public decimal? DiscountPercentage { get; set; }

    [Column(TypeName = "decimal(18, 4)")]
    public decimal DiscountTotal { get; set; }

    public bool IsActive { get; set; }

    public long CreatedByUserId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime CreatedDate { get; set; }

    public long UpdatedByUserId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime UpdatedDate { get; set; }

    [ForeignKey("CompanyId")]
    [InverseProperty("PatientRegistrations")]
    public virtual Company? Company { get; set; }

    [ForeignKey("PackageId")]
    [InverseProperty("PatientRegistrations")]
    public virtual Package? Package { get; set; }

    [ForeignKey("PatientId")]
    [InverseProperty("PatientRegistrations")]
    public virtual Patient Patient { get; set; } = null!;

    [InverseProperty("PatientRegistration")]
    public virtual ICollection<PatientRegistrationService> PatientRegistrationServices { get; set; } = new List<PatientRegistrationService>();

    [InverseProperty("PatientRegistration")]
    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();
}
