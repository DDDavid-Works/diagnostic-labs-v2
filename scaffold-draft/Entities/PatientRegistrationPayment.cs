using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Infrastructure.Persistence.Scaffolded.Entities;

[Keyless]
public partial class PatientRegistrationPayment
{
    public long PatientRegistrationId { get; set; }

    [Column(TypeName = "decimal(18, 4)")]
    public decimal AmountDue { get; set; }

    [Column(TypeName = "decimal(38, 4)")]
    public decimal AmountPaid { get; set; }

    [Column(TypeName = "decimal(38, 4)")]
    public decimal? Balance { get; set; }

    public bool IsCharge { get; set; }
}
