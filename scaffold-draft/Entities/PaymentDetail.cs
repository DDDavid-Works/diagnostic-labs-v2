using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Infrastructure.Persistence.Scaffolded.Entities;

[Keyless]
public partial class PaymentDetail
{
    public long PaymentId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime PaymentDate { get; set; }

    public long PatientRegistrationId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime PatientRegistrationDate { get; set; }

    public long PatientId { get; set; }

    [StringLength(200)]
    public string PatientName { get; set; } = null!;

    public long? CompanyId { get; set; }

    [StringLength(200)]
    public string? CompanyName { get; set; }
}
