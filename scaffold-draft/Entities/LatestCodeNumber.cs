using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Infrastructure.Persistence.Scaffolded.Entities;

[Keyless]
public partial class LatestCodeNumber
{
    [StringLength(100)]
    public string Prefix { get; set; } = null!;

    public int? MaxNumber { get; set; }
}
