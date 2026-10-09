using System.Net.Mail;
using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Domain.Settings;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Application.Settings;

public sealed record CompanySetupDetails(
    long Id,
    string? CompanyName,
    string? SubCompanyName,
    string? Tagline,
    string? Address,
    string? ContactNumbers,
    string? Email,
    string? Code,
    byte[]? Logo,
    byte[] RowVersion);

/// <summary>The whole screen is saved at once; <see cref="Logo"/> is the logo to keep (<c>null</c> removes it).</summary>
public sealed record CompanySetupInput(
    long Id,
    string? CompanyName,
    string? SubCompanyName,
    string? Tagline,
    string? Address,
    string? ContactNumbers,
    string? Email,
    string? Code,
    byte[]? Logo,
    byte[]? RowVersion);

/// <summary>What the sign-in window shows before anybody has signed in.</summary>
public sealed record CompanyBranding(string? CompanyName, string? SubCompanyName, string? Tagline, byte[]? Logo);

public interface ICompanySetupService
{
    Task<Result<CompanySetupDetails>> GetAsync(CancellationToken cancellationToken = default);

    Task<Result<CompanySetupDetails>> SaveAsync(CompanySetupInput input, CancellationToken cancellationToken = default);

    /// <summary>Name, tagline and logo for the sign-in window. Needs no permission: nothing here is confidential.</summary>
    Task<CompanyBranding> GetBrandingAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// The laboratory's own details, printed on reports and shown at sign-in. There is one logical record: the newest
/// row wins (older databases may hold several), and saving updates it or creates it if none exists yet.
/// </summary>
public sealed class CompanySetupService(IAppDbContext db, ICurrentUser currentUser) : ICompanySetupService
{
    public const int LogoMaxBytes = 2 * 1024 * 1024;
    public const int CodeMaxLength = 20;

    public async Task<Result<CompanySetupDetails>> GetAsync(CancellationToken cancellationToken = default)
    {
        if (!currentUser.Can(ModuleIds.CompanySetup, ModuleAction.View))
            return Result<CompanySetupDetails>.Failure(Errors.Forbidden);

        var row = await Latest().AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        return Result<CompanySetupDetails>.Success(row is null ? Empty() : ToDetails(row));
    }

    public async Task<Result<CompanySetupDetails>> SaveAsync(CompanySetupInput input, CancellationToken cancellationToken = default)
    {
        if (!currentUser.Can(ModuleIds.CompanySetup, ModuleAction.Edit))
            return Result<CompanySetupDetails>.Failure(Errors.Forbidden);

        var errors = Validate(input);
        if (errors.Count > 0)
            return Result<CompanySetupDetails>.Failure(Errors.Invalid(errors));

        var row = await Latest().FirstOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            row = new CompanySetup();
            db.CompanySetups.Add(row);
        }
        else if (input.RowVersion is { Length: > 0 })
        {
            db.SetOriginalRowVersion(row, input.RowVersion);
        }

        row.CompanyName = Clean(input.CompanyName);
        row.SubCompanyName = Clean(input.SubCompanyName);
        row.Tagline = Clean(input.Tagline);
        row.Address = Clean(input.Address);
        row.ContactNumbers = Clean(input.ContactNumbers);
        row.Email = Clean(input.Email);
        row.Code = Clean(input.Code)?.ToUpperInvariant();
        row.Logo = input.Logo is { Length: > 0 } ? input.Logo : null;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<CompanySetupDetails>.Failure(Errors.Conflict("Company setup"));
        }

        return Result<CompanySetupDetails>.Success(ToDetails(row));
    }

    public async Task<CompanyBranding> GetBrandingAsync(CancellationToken cancellationToken = default)
    {
        var row = await Latest().AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        return row is null ? new CompanyBranding(null, null, null, null) : new CompanyBranding(row.CompanyName, row.SubCompanyName, row.Tagline, row.Logo);
    }

    public static IReadOnlyList<string> Validate(CompanySetupInput input)
    {
        var errors = new List<string>();

        Required(errors, input.CompanyName, "Company name", 200);
        Optional(errors, input.SubCompanyName, "Subcompany name", 200);
        Optional(errors, input.Tagline, "Tagline", 500);
        Optional(errors, input.Address, "Address", 200);
        Optional(errors, input.ContactNumbers, "Contact numbers", 200);
        Optional(errors, input.Email, "Email address", 200);

        var email = input.Email?.Trim();
        if (!string.IsNullOrEmpty(email) && !IsEmail(email))
            errors.Add("Email address is not valid.");

        // The code is the prefix of every registration code, which are split on '-', so it can not contain one.
        var code = input.Code?.Trim();
        if (!string.IsNullOrEmpty(code))
        {
            if (code.Length > CodeMaxLength)
                errors.Add($"Company code can not be longer than {CodeMaxLength} characters.");
            else if (!code.All(char.IsAsciiLetterOrDigit))
                errors.Add("Company code can only contain letters and numbers.");
        }

        if (input.Logo is { Length: > 0 } logo)
        {
            if (logo.Length > LogoMaxBytes)
                errors.Add($"The logo is too large; the limit is {LogoMaxBytes / (1024 * 1024)} MB.");
            else if (!LooksLikeImage(logo))
                errors.Add("The logo must be a PNG, JPG, GIF or BMP image.");
        }

        return errors;
    }

    private IQueryable<CompanySetup> Latest() =>
        db.CompanySetups.OrderByDescending(c => c.UpdatedAtUtc).ThenByDescending(c => c.Id);

    private static CompanySetupDetails Empty() => new(0, null, null, null, null, null, null, null, null, []);

    private static CompanySetupDetails ToDetails(CompanySetup c) =>
        new(c.Id, c.CompanyName, c.SubCompanyName, c.Tagline, c.Address, c.ContactNumbers, c.Email, c.Code, c.Logo, c.RowVersion);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void Required(List<string> errors, string? value, string label, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
            errors.Add($"{label} can not be empty.");
        else
            Optional(errors, value, label, max);
    }

    private static void Optional(List<string> errors, string? value, string label, int max)
    {
        if ((value?.Trim().Length ?? 0) > max)
            errors.Add($"{label} can not be longer than {max} characters.");
    }

    private static bool IsEmail(string email) =>
        MailAddress.TryCreate(email, out var parsed) && parsed.Address == email && email.Contains('.', StringComparison.Ordinal);

    // Recognise the formats a report can print by their first bytes, so a renamed .exe or .pdf is not stored as a "logo".
    private static bool LooksLikeImage(byte[] b) =>
        (b.Length > 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47)  // PNG
        || (b.Length > 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF)                // JPEG
        || (b.Length > 6 && b[0] == 0x47 && b[1] == 0x49 && b[2] == 0x46 && b[3] == 0x38) // GIF
        || (b.Length > 2 && b[0] == 0x42 && b[1] == 0x4D);                               // BMP
}
