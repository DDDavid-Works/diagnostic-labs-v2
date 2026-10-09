using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Settings;
using DiagnosticLabs.Domain.Settings;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Application.Tests.Settings;

public class CompanySetupServiceTests
{
    // Smallest byte sequences that start like each supported image format.
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00];
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10];
    private static readonly byte[] Pdf = "%PDF-1.7 not an image"u8.ToArray();

    private readonly TestEnvironment _env = new();

    private void SignInAsAdmin() => _env.Session.SignIn(new AuthenticatedUser(1, "admin", "Admin", true, false, []));

    private static CompanySetupInput Input(string? name = "Bayside Diagnostic Center", string? code = "BADC", byte[]? logo = null, string? email = "lab@example.com") =>
        new(0, name, "Main Branch", "Results you can trust", "1 Harbor Road", "0917 000 0000", email, code, logo, null);

    [Fact]
    public async Task Before_anything_is_saved_the_screen_gets_an_empty_record()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();

        var result = await new CompanySetupService(db, _env.Session).GetAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value.Id);
        Assert.Null(result.Value.CompanyName);
    }

    [Fact]
    public async Task Saving_creates_the_record_and_saving_again_updates_the_same_one()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var service = new CompanySetupService(db, _env.Session);

        var first = (await service.SaveAsync(Input())).Value;
        var second = (await service.SaveAsync(Input("Renamed Center") with { Id = first.Id })).Value;

        Assert.Equal(first.Id, second.Id);
        Assert.Equal("Renamed Center", second.CompanyName);
        Assert.Equal(1, await db.CompanySetups.CountAsync());
    }

    [Fact]
    public async Task When_older_databases_hold_several_rows_the_newest_one_is_used_and_updated()
    {
        await using var db = _env.CreateDb();
        db.CompanySetups.Add(new CompanySetup { CompanyName = "Old name", Code = "OLD" });
        await db.SaveChangesAsync();
        _env.Clock.UtcNow += TimeSpan.FromDays(1);
        db.CompanySetups.Add(new CompanySetup { CompanyName = "Current name", Code = "CUR" });
        await db.SaveChangesAsync();
        SignInAsAdmin();
        var service = new CompanySetupService(db, _env.Session);

        var loaded = (await service.GetAsync()).Value;
        await service.SaveAsync(Input("Updated name"));

        Assert.Equal("Current name", loaded.CompanyName);
        Assert.Equal(["Old name", "Updated name"], (await db.CompanySetups.OrderBy(c => c.Id).Select(c => c.CompanyName).ToListAsync())!);
    }

    [Fact]
    public async Task The_company_code_is_stored_in_upper_case_and_may_only_hold_letters_and_numbers()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var service = new CompanySetupService(db, _env.Session);

        var saved = await service.SaveAsync(Input(code: "  badc7 "));
        var withHyphen = await service.SaveAsync(Input(code: "BA-DC"));
        var withSpace = await service.SaveAsync(Input(code: "BA DC"));
        var tooLong = await service.SaveAsync(Input(code: new string('A', CompanySetupService.CodeMaxLength + 1)));

        Assert.Equal("BADC7", saved.Value.Code);
        Assert.True(withHyphen.IsFailure);
        Assert.True(withSpace.IsFailure);
        Assert.True(tooLong.IsFailure);
    }

    [Fact]
    public async Task The_company_code_may_be_left_empty()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();

        var saved = await new CompanySetupService(db, _env.Session).SaveAsync(Input(code: "  "));

        Assert.True(saved.IsSuccess);
        Assert.Null(saved.Value.Code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task The_company_name_is_required(string? name)
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();

        var result = await new CompanySetupService(db, _env.Session).SaveAsync(Input(name));

        Assert.True(result.IsFailure);
        Assert.Contains("Company name", result.Error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("two@@example.com")]
    [InlineData("a b@example.com")]
    [InlineData("name@localhost")]
    public async Task A_malformed_email_is_refused(string email)
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();

        Assert.True((await new CompanySetupService(db, _env.Session).SaveAsync(Input(email: email))).IsFailure);
    }

    [Fact]
    public async Task The_email_is_optional()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();

        Assert.True((await new CompanySetupService(db, _env.Session).SaveAsync(Input(email: null))).IsSuccess);
    }

    [Fact]
    public async Task Over_long_text_is_refused()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();

        var result = await new CompanySetupService(db, _env.Session).SaveAsync(Input() with { Address = new string('x', 201) });

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task A_logo_must_be_a_real_image_within_the_size_limit()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var service = new CompanySetupService(db, _env.Session);

        Assert.True((await service.SaveAsync(Input(logo: Png))).IsSuccess);
        Assert.True((await service.SaveAsync(Input(logo: Jpeg))).IsSuccess);
        Assert.True((await service.SaveAsync(Input(logo: Pdf))).IsFailure);

        var big = new byte[CompanySetupService.LogoMaxBytes + 1];
        Png.CopyTo(big, 0);
        Assert.True((await service.SaveAsync(Input(logo: big))).IsFailure);
    }

    [Fact]
    public async Task The_logo_round_trips_and_can_be_removed()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        var service = new CompanySetupService(db, _env.Session);

        var withLogo = (await service.SaveAsync(Input(logo: Png))).Value;
        Assert.Equal(Png, (await service.GetAsync()).Value.Logo);

        var removed = (await service.SaveAsync(Input(logo: null) with { Id = withLogo.Id })).Value;
        Assert.Null(removed.Logo);
        Assert.Null((await service.GetAsync()).Value.Logo);
    }

    [Fact]
    public async Task Viewing_needs_view_access_and_saving_needs_edit_access()
    {
        await using var db = _env.CreateDb();
        var service = new CompanySetupService(db, _env.Session);

        _env.Session.SignIn(new AuthenticatedUser(2, "viewer", "Viewer", false, false, [new ModulePermission(ModuleIds.CompanySetup, false, false, false, false)]));
        Assert.True((await service.GetAsync()).IsSuccess);
        Assert.Equal("Auth.Forbidden", (await service.SaveAsync(Input())).Error.Code);

        _env.Session.SignIn(new AuthenticatedUser(3, "nobody", "Nobody", false, false, []));
        Assert.Equal("Auth.Forbidden", (await service.GetAsync()).Error.Code);
    }

    [Fact]
    public async Task The_sign_in_window_can_read_the_branding_without_anyone_being_signed_in()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();
        await new CompanySetupService(db, _env.Session).SaveAsync(Input(logo: Png));
        _env.Session.SignOut();

        var branding = await new CompanySetupService(db, _env.Session).GetBrandingAsync();

        Assert.Equal("Bayside Diagnostic Center", branding.CompanyName);
        Assert.Equal("Results you can trust", branding.Tagline);
        Assert.Equal(Png, branding.Logo);
    }

    [Fact]
    public async Task Branding_is_empty_rather_than_an_error_before_setup()
    {
        await using var db = _env.CreateDb();

        var branding = await new CompanySetupService(db, _env.Session).GetBrandingAsync();

        Assert.Null(branding.CompanyName);
        Assert.Null(branding.Logo);
    }

    [Fact]
    public async Task Saving_is_recorded_in_the_audit_log_without_dumping_the_logo_bytes()
    {
        await using var db = _env.CreateDb();
        SignInAsAdmin();

        await new CompanySetupService(db, _env.Session).SaveAsync(Input(logo: Png));

        var log = await db.AuditLogs.SingleAsync(a => a.EntityName == nameof(CompanySetup));
        Assert.Contains("[binary", log.NewValues, StringComparison.Ordinal);
        Assert.DoesNotContain("137", log.NewValues!.Replace("[binary 10 bytes]", string.Empty), StringComparison.Ordinal);
    }
}
