using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Infrastructure.Persistence.Scaffolded.Entities;

public partial class Payment
{
    [Key]
    public long Id { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime PaymentDate { get; set; }

    public long PatientRegistrationId { get; set; }

    [Column(TypeName = "decimal(18, 4)")]
    public decimal PaymentAmount { get; set; }

    public bool IsActive { get; set; }

    public long CreatedByUserId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime CreatedDate { get; set; }

    public long UpdatedByUserId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime UpdatedDate { get; set; }

    public bool IsCharge { get; set; }

    [ForeignKey("PatientRegistrationId")]
    [InverseProperty("Payments")]
    public virtual PatientRegistration PatientRegistration { get; set; } = null!;
}
