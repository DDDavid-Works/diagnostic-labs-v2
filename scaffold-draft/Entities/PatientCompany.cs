using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Infrastructure.Persistence.Scaffolded.Entities;

[Keyless]
public partial class PatientCompany
{
    public long PatientId { get; set; }

    [StringLength(200)]
    public string PatientCode { get; set; } = null!;

    [StringLength(200)]
    public string PatientName { get; set; } = null!;

    public long? CompanyId { get; set; }

    [StringLength(200)]
    public string CompanyName { get; set; } = null!;
}
