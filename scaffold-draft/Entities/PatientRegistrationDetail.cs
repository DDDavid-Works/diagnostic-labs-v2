using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Infrastructure.Persistence.Scaffolded.Entities;

[Keyless]
public partial class PatientRegistrationDetail
{
    public long PatientRegistrationId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime InputDate { get; set; }

    [Column(TypeName = "decimal(18, 4)")]
    public decimal AmountDue { get; set; }

    public long? PatientId { get; set; }

    [StringLength(200)]
    public string? PatientName { get; set; }

    public long? CompanyId { get; set; }

    [StringLength(200)]
    public string? CompanyName { get; set; }
}
