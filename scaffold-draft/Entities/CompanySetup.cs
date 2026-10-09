using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Infrastructure.Persistence.Scaffolded.Entities;

public partial class CompanySetup
{
    [Key]
    public long Id { get; set; }

    [StringLength(200)]
    public string? CompanyName { get; set; }

    [StringLength(200)]
    public string? SubCompanyName { get; set; }

    [StringLength(500)]
    public string? Tagline { get; set; }

    [StringLength(200)]
    public string? Address { get; set; }

    [StringLength(200)]
    public string? ContactNumbers { get; set; }

    [StringLength(200)]
    public string? Email { get; set; }

    [StringLength(20)]
    public string? Code { get; set; }

    public byte[]? Logo { get; set; }

    public long UpdatedByUserId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime UpdatedDate { get; set; }
}
