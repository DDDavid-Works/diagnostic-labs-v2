using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Application.LabResults;
using DiagnosticLabs.Domain.Settings;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Application.Entries;

/// <summary>One person of a signatory list on the Signatures screen: name, licence and the stored signature (if any).</summary>
public sealed record SignatoryRow(long EntryId, string ListName, string Name, string? LicenseNo, byte[]? Signature);

/// <summary>The limits of a stored signature. The screen shrinks and trims a picked file to fit before it is saved.</summary>
public static class SignatureImages
{
    public const int MaxWidth = 600;
    public const int MaxHeight = 200;

    /// <summary>The largest stored PNG; a prepared signature of 600 x 200 is far below it.</summary>
    public const int MaxBytes = 512 * 1024;

    private static readonly byte[] PngHeader = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public static bool IsPng(byte[] bytes) => bytes.Length > PngHeader.Length && bytes.AsSpan(0, PngHeader.Length).SequenceEqual(PngHeader);
}

public interface ISignatureService
{
    /// <summary>Everyone on the Medical Technologist, Pathologist and Physician lists with their signature (administrators: the Signatures screen).</summary>
    Task<Result<IReadOnlyList<SignatoryRow>>> GetSignatoriesAsync(CancellationToken cancellationToken = default);

    /// <summary>Stores (or replaces) the signature of one signatory entry. <paramref name="png"/> is the prepared image.</summary>
    Task<Result> SetAsync(long entryId, byte[] png, CancellationToken cancellationToken = default);

    Task<Result> RemoveAsync(long entryId, CancellationToken cancellationToken = default);
}

/// <summary>
/// The signatures printed above the signatories' names. They belong to the entries of the signatory lists of the Entry Builder and are
/// managed on the Signatures screen; printing looks them up by name (<see cref="SignatureStamp"/>).
/// </summary>
public sealed class SignatureService(IAppDbContext db, ICurrentUser currentUser) : ISignatureService
{
    public async Task<Result<IReadOnlyList<SignatoryRow>>> GetSignatoriesAsync(CancellationToken cancellationToken = default)
    {
        if (!currentUser.Can(ModuleIds.Signatures, ModuleAction.View))
            return Result<IReadOnlyList<SignatoryRow>>.Failure(Errors.Forbidden);

        var names = EntryFields.Signatories.Select(f => f.Name).ToList();
        var entries = await db.LookupValues.AsNoTracking()
            .Where(l => l.Kind == LookupKind.SingleLine && l.ModuleId == null && l.IsActive && names.Contains(l.FieldName))
            .OrderBy(l => l.Id)
            .ToListAsync(cancellationToken);
        var ids = entries.Select(e => e.Id).ToList();
        var images = await db.SignatoryImages.AsNoTracking().Where(s => ids.Contains(s.LookupValueId)).ToDictionaryAsync(s => s.LookupValueId, s => s.Image, cancellationToken);

        var rows = EntryFields.Signatories
            .SelectMany(field => entries
                .Where(e => e.FieldName == field.Name)
                .Select(e => new SignatoryRow(e.Id, field.Name, e.Value, e.LicenseNo, images.TryGetValue(e.Id, out var image) ? image : null)))
            .ToList();
        return Result<IReadOnlyList<SignatoryRow>>.Success(rows);
    }

    public async Task<Result> SetAsync(long entryId, byte[] png, CancellationToken cancellationToken = default)
    {
        if (!currentUser.Can(ModuleIds.Signatures, ModuleAction.Edit))
            return Result.Failure(Errors.Forbidden);

        if (png is not { Length: > 0 } || !SignatureImages.IsPng(png))
            return Result.Failure(Errors.Invalid(["The signature must be a PNG picture."]));
        if (png.Length > SignatureImages.MaxBytes)
            return Result.Failure(Errors.Invalid([$"The signature picture can not be larger than {SignatureImages.MaxBytes / 1024} KB."]));

        if (!await IsSignatoryAsync(entryId, cancellationToken))
            return Result.Failure(Errors.NotFound("Signatory"));

        var image = await db.SignatoryImages.FirstOrDefaultAsync(s => s.LookupValueId == entryId, cancellationToken);
        if (image is null)
        {
            image = new SignatoryImage { LookupValueId = entryId };
            db.SignatoryImages.Add(image);
        }

        image.Image = png;
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> RemoveAsync(long entryId, CancellationToken cancellationToken = default)
    {
        if (!currentUser.Can(ModuleIds.Signatures, ModuleAction.Edit))
            return Result.Failure(Errors.Forbidden);

        var image = await db.SignatoryImages.FirstOrDefaultAsync(s => s.LookupValueId == entryId, cancellationToken);
        if (image is not null)
        {
            db.SignatoryImages.Remove(image);
            await db.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }

    private Task<bool> IsSignatoryAsync(long entryId, CancellationToken cancellationToken)
    {
        var names = EntryFields.Signatories.Select(f => f.Name).ToList();
        return db.LookupValues.AnyAsync(
            l => l.Id == entryId && l.Kind == LookupKind.SingleLine && l.ModuleId == null && l.IsActive && names.Contains(l.FieldName), cancellationToken);
    }
}

/// <summary>
/// Puts the stored signatures on a printout. A report keeps the signatories' names as text, so each name is looked up in its own list first
/// (a physician in the Physician list) and then in the other signatory lists, so a person on two lists needs only one upload.
/// The signature on file now is printed, also on a reprint of an older result. A name that is on no list prints without a signature.
/// </summary>
public static class SignatureStamp
{
    public static async Task<PrintableReport> AttachAsync(IAppDbContext db, PrintableReport report, CancellationToken cancellationToken = default)
    {
        var people = Pages(report).SelectMany(p => p.Signatories).Where(s => !string.IsNullOrWhiteSpace(s.Name)).ToList();
        if (people.Count == 0)
            return report;

        var listNames = EntryFields.Signatories.Select(f => f.Name).ToList();
        var signed = await (
                from l in db.LookupValues.AsNoTracking()
                join s in db.SignatoryImages.AsNoTracking() on l.Id equals s.LookupValueId
                where l.Kind == LookupKind.SingleLine && l.ModuleId == null && l.IsActive && listNames.Contains(l.FieldName)
                orderby l.Id
                select new { l.FieldName, l.Value, s.Image })
            .ToListAsync(cancellationToken);
        if (signed.Count == 0)
            return report;

        byte[]? Find(PrintSignatory person)
        {
            var name = person.Name!.Trim();
            var matches = signed.Where(s => string.Equals(s.Value.Trim(), name, StringComparison.OrdinalIgnoreCase)).ToList();
            return (matches.FirstOrDefault(m => m.FieldName == person.List?.Name) ?? matches.FirstOrDefault())?.Image;
        }

        return Stamp(report, Find);
    }

    private static PrintableReport Stamp(PrintableReport report, Func<PrintSignatory, byte[]?> find) => report with
    {
        Signatories = [.. report.Signatories.Select(s => string.IsNullOrWhiteSpace(s.Name) ? s : s with { Signature = find(s) })],
        NextPage = report.NextPage is { } next ? Stamp(next, find) : null,
    };

    private static IEnumerable<PrintableReport> Pages(PrintableReport report)
    {
        for (var page = report; page is not null; page = page.NextPage)
            yield return page;
    }
}
