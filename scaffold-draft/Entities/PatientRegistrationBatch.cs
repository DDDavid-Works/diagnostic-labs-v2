using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Infrastructure.Persistence.Scaffolded.Entities;

[Keyless]
public partial class PatientRegistrationBatch
{
    public long? CompanyId { get; set; }

    [StringLength(200)]
    public string BatchName { get; set; } = null!;
}
