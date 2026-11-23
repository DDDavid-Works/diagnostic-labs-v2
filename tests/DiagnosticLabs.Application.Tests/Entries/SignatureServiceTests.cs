using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Application.Entries;
using DiagnosticLabs.Application.LabResults;
using DiagnosticLabs.Domain.Settings;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Application.Tests.Entries;

public class SignatureServiceTests
{
    private readonly TestEnvironment _env = new();

    public SignatureServiceTests() => _env.Clock.UtcNow = new DateTime(2026, 11, 23, 4, 0, 0, DateTimeKind.Utc);

    private static byte[] Png(byte mark) => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, mark, 1, 2, 3];

    private void SignInAsAdmin() => _env.Session.SignIn(new AuthenticatedUser(1, "admin", "Admin", true, false, []));

    private void SignInWith(params ModulePermission[] permissions) => _env.Session.SignIn(new AuthenticatedUser(3, "u", "U", false, false, permissions));

    private static async Task<LookupValue> AddAsync(Infrastructure.Persistence.AppDbContext db, EntryField list, string name, string? license = null, bool active = true)
    {
        var entry = new LookupValue { Kind = LookupKind.SingleLine, FieldName = list.Name, ModuleId = list.ModuleId, Value = name, LicenseNo = license, IsActive = active };
        db.LookupValues.Add(entry);
        await db.SaveChangesAsync();
        return entry;
    }

    private static async Task SignAsync(Infrastructure.Persistence.AppDbContext db, LookupValue entry, byte[] png)
    {
        db.SignatoryImages.Add(new SignatoryImage { LookupValueId = entry.Id, Image = png });
        await db.SaveChangesAsync();
    }

    private static PrintableReport Report(IReadOnlyList<PrintSignatory> signatories, PrintableReport? next = null) => new(
        ReportLayout.Hematology, "Hematology", new ReportLetterhead("Lab", null, null, null, null, null), [], [], [], signatories, string.Empty, NextPage: next);

    // ---------------------------------------------------------------- the Signatures screen

    [Fact]
    public async Task The_screen_lists_the_three_signatory_lists_in_order_with_their_signatures()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var physician = await AddAsync(db, EntryFields.Physician, "DR. MALONZO", "87844");
        var tech = await AddAsync(db, EntryFields.MedicalTechnologist, "ANA REYES", "0012345");
        await AddAsync(db, EntryFields.Pathologist, "DR. FIORA", "567655");
        await AddAsync(db, EntryFields.Pathologist, "DR. GONE", active: false);
        await AddAsync(db, EntryFields.Gender, "Female");
        await SignAsync(db, tech, Png(7));

        var rows = (await new SignatureService(db, _env.Session).GetSignatoriesAsync()).Value;

        Assert.Equal(["ANA REYES", "DR. FIORA", "DR. MALONZO"], rows.Select(r => r.Name));
        Assert.Equal(["Medical Technologist", "Pathologist", "Physician"], rows.Select(r => r.ListName));
        Assert.Equal(Png(7), rows[0].Signature);
        Assert.Null(rows[2].Signature);
        Assert.Equal(physician.Id, rows[2].EntryId);
        Assert.Equal("87844", rows[2].LicenseNo);
    }

    [Fact]
    public async Task A_signature_is_stored_replaced_and_removed()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var service = new SignatureService(db, _env.Session);
        var entry = await AddAsync(db, EntryFields.Pathologist, "DR. FIORA");

        Assert.True((await service.SetAsync(entry.Id, Png(1))).IsSuccess);
        Assert.True((await service.SetAsync(entry.Id, Png(2))).IsSuccess);
        Assert.Equal(Png(2), (await db.SignatoryImages.AsNoTracking().SingleAsync()).Image);

        Assert.True((await service.RemoveAsync(entry.Id)).IsSuccess);
        Assert.Empty(await db.SignatoryImages.ToListAsync());
        Assert.True((await service.RemoveAsync(entry.Id)).IsSuccess); // nothing to remove is fine
    }

    [Fact]
    public async Task Only_a_png_of_a_reasonable_size_for_an_active_signatory_is_accepted()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var service = new SignatureService(db, _env.Session);
        var entry = await AddAsync(db, EntryFields.MedicalTechnologist, "ANA REYES");
        var gender = await AddAsync(db, EntryFields.Gender, "Female");
        var gone = await AddAsync(db, EntryFields.Physician, "DR. GONE", active: false);

        Assert.Equal("Validation.Invalid", (await service.SetAsync(entry.Id, [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3, 4, 5])).Error.Code); // a JPEG
        Assert.Equal("Validation.Invalid", (await service.SetAsync(entry.Id, [])).Error.Code);
        var huge = new byte[SignatureImages.MaxBytes + 1];
        Png(0).CopyTo(huge, 0);
        Assert.Equal("Validation.Invalid", (await service.SetAsync(entry.Id, huge)).Error.Code);
        Assert.Equal("Signatory.NotFound", (await service.SetAsync(gender.Id, Png(1))).Error.Code);
        Assert.Equal("Signatory.NotFound", (await service.SetAsync(gone.Id, Png(1))).Error.Code);
        Assert.Equal("Signatory.NotFound", (await service.SetAsync(999, Png(1))).Error.Code);
        Assert.Empty(await db.SignatoryImages.ToListAsync());
    }

    [Fact]
    public async Task Managing_signatures_needs_the_Signatures_module()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var entry = await AddAsync(db, EntryFields.Pathologist, "DR. FIORA");
        await SignAsync(db, entry, Png(1));

        // Someone who can print results, but has no Signatures permission.
        SignInWith(new ModulePermission(ModuleIds.Hematology, true, true, true, true));
        var service = new SignatureService(db, _env.Session);
        Assert.Equal(Errors.Forbidden, (await service.GetSignatoriesAsync()).Error);
        Assert.Equal(Errors.Forbidden, (await service.SetAsync(entry.Id, Png(2))).Error);
        Assert.Equal(Errors.Forbidden, (await service.RemoveAsync(entry.Id)).Error);

        // View only: can look, can not change.
        SignInWith(new ModulePermission(ModuleIds.Signatures, false, false, false, false));
        Assert.True((await service.GetSignatoriesAsync()).IsSuccess);
        Assert.Equal(Errors.Forbidden, (await service.SetAsync(entry.Id, Png(2))).Error);

        Assert.Equal(Png(1), (await db.SignatoryImages.AsNoTracking().SingleAsync()).Image);
    }

    // ---------------------------------------------------------------- on the printout

    [Fact]
    public async Task Each_name_gets_the_signature_of_its_own_list_first_and_of_another_list_otherwise()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        await SignAsync(db, await AddAsync(db, EntryFields.Pathologist, "DR. FIORA"), Png(1));
        await SignAsync(db, await AddAsync(db, EntryFields.Physician, "DR. FIORA"), Png(2));
        await SignAsync(db, await AddAsync(db, EntryFields.MedicalTechnologist, "Ana Reyes"), Png(3));
        await AddAsync(db, EntryFields.MedicalTechnologist, "BEN CRUZ"); // on the list, no signature

        var report = Report(
        [
            new("Medical Technologist", "  ana reyes ", null, EntryFields.MedicalTechnologist),
            new("Medical Technologist", "BEN CRUZ", null, EntryFields.MedicalTechnologist),
            new("Pathologist", "DR. FIORA", null, EntryFields.Pathologist),
        ],
        Report([new("Physician", "DR. FIORA", null, EntryFields.Physician), new("Noted by", "DR. FIORA", null, EntryFields.MedicalTechnologist)]));

        var stamped = await SignatureStamp.AttachAsync(db, report);

        Assert.Equal(Png(3), stamped.Signatories[0].Signature);   // name matched without case or spaces
        Assert.Null(stamped.Signatories[1].Signature);
        Assert.Equal(Png(1), stamped.Signatories[2].Signature);   // the Pathologist entry, not the Physician one
        Assert.Equal(Png(2), stamped.NextPage!.Signatories[0].Signature);
        Assert.Equal(Png(1), stamped.NextPage.Signatories[1].Signature); // not a technologist: the first list that has the name
        Assert.True(stamped.HasSignatures);
    }

    [Fact]
    public async Task A_name_on_no_list_a_blank_name_or_a_removed_entry_prints_without_a_signature()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        await SignAsync(db, await AddAsync(db, EntryFields.Pathologist, "DR. GONE", active: false), Png(1));
        await SignAsync(db, await AddAsync(db, EntryFields.Gender, "DR. FIORA"), Png(2)); // not a signatory list

        var stamped = await SignatureStamp.AttachAsync(db, Report(
        [
            new("Medical Technologist", "SOMEONE TYPED", null, EntryFields.MedicalTechnologist),
            new("Medical Technologist", null, null, EntryFields.MedicalTechnologist),
            new("Pathologist", "DR. GONE", null, EntryFields.Pathologist),
            new("Pathologist", "DR. FIORA", null, EntryFields.Pathologist),
        ]));

        Assert.All(stamped.Signatories, s => Assert.Null(s.Signature));
        Assert.False(stamped.HasSignatures);
    }

    [Fact]
    public void A_report_can_be_printed_without_its_signatures()
    {
        var report = Report(
            [new("Pathologist", "DR. FIORA", "1", EntryFields.Pathologist, Png(1))],
            Report([new("Physician", "DR. MALONZO", "2", EntryFields.Physician, Png(2))]));

        var plain = report.WithoutSignatures();

        Assert.True(report.HasSignatures);
        Assert.False(plain.HasSignatures);
        Assert.Equal("DR. FIORA", plain.Signatories[0].Name);
        Assert.Equal("2", plain.NextPage!.Signatories[0].LicenseNo);
        Assert.NotNull(report.Signatories[0].Signature); // the original is unchanged
    }

    [Fact]
    public async Task A_result_prints_with_the_signatures_on_file_now()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var service = new HematologyService(db, _env.Session, _env.Clock);
        var header = new LabResultHeader(null, null, null, "Elena Castillo", "35", "Female", null, LocalTime.ToLocalDate(_env.Clock.UtcNow), null, "ANA REYES", "DR. FIORA", MedicalTechnologist2: "BEN CRUZ");
        var saved = (await service.SaveAsync(new HematologyInput(0, header, new HematologyData(), null))).Value;

        // The signature is uploaded after the result was saved: a reprint has it.
        await SignAsync(db, await AddAsync(db, EntryFields.Pathologist, "DR. FIORA"), Png(9));
        var printable = (await service.GetPrintableAsync(saved.Id)).Value;

        Assert.Equal(["ANA REYES", "BEN CRUZ", "DR. FIORA"], printable.Signatories.Select(s => s.Name));
        Assert.Equal([null, null, Png(9)], printable.Signatories.Select(s => s.Signature));
        Assert.Equal([EntryFields.MedicalTechnologist, EntryFields.MedicalTechnologist, EntryFields.Pathologist], printable.Signatories.Select(s => s.List));
    }
}
