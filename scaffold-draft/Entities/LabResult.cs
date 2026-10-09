using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Infrastructure.Persistence.Scaffolded.Entities;

[Keyless]
public partial class LabResult
{
    public long? RowNumber { get; set; }

    [StringLength(20)]
    [Unicode(false)]
    public string Service { get; set; } = null!;

    public long Id { get; set; }

    public long? PatientRegistrationId { get; set; }

    public long? PatientId { get; set; }

    [StringLength(200)]
    public string? PatientCode { get; set; }

    [StringLength(200)]
    public string PatientName { get; set; } = null!;

    public long? CompanyId { get; set; }

    [StringLength(200)]
    public string? Company { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? DateRequested { get; set; }

    public bool IsActive { get; set; }
}
