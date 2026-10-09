using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Entries;
using DiagnosticLabs.Domain.Identity;
using DiagnosticLabs.Domain.Settings;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Application.Tests.Entries;

public class EntryServiceTests
{
    private const int Patients = ModuleIds.Patients;
    private const int Serology = 9;
    private static readonly EntryField SerologyTest = new("Test", EntryKind.SingleLine, Serology);
    private static readonly EntryField ImmunologyTest = new("Test", EntryKind.SingleLine, 8);
    private static readonly EntryField HematologyResult = new("Result", EntryKind.MultiLine, 7);

    private readonly TestEnvironment _env = new();

    private void SignIn(bool canEdit = true, int moduleId = Patients) =>
        _env.Session.SignIn(new AuthenticatedUser(
            3, "u", "U", false, false, [new ModulePermission(moduleId, canEdit, canEdit, canEdit, false)]));

    private static List<SingleLineEntry> Lines(params string[] values) => [.. values.Select(v => new SingleLineEntry(0, v))];

    [Fact]
    public async Task Entries_keep_the_order_they_were_added_in()
    {
        await using var db = _env.CreateDb();
        SignIn();
        var service = new EntryService(db, _env.Session);

        await service.SaveSingleLineAsync(EntryFields.Gender, Patients, Lines("Male", "Female", "Other"));

        Assert.Equal(["Male", "Female", "Other"], await service.GetChoicesAsync(EntryFields.Gender));
    }

    [Fact]
    public async Task Renaming_keeps_the_position_and_removing_switches_the_row_off()
    {
        await using var db = _env.CreateDb();
        SignIn();
        var service = new EntryService(db, _env.Session);
        var saved = (await service.SaveSingleLineAsync(EntryFields.CivilStatus, Patients, Lines("Single", "Married", "Widowed"))).Value;

        var items = saved.Items.ToList();
        items[0] = items[0] with { Value = "Never married" };
        items.RemoveAt(1); // drop Married
        items.Add(new SingleLineEntry(0, "Separated"));
        var result = await service.SaveSingleLineAsync(EntryFields.CivilStatus, Patients, items);

        Assert.Equal(["Never married", "Widowed", "Separated"], result.Value.Items.Select(i => i.Value));
        Assert.Equal(saved.Items[0].Id, result.Value.Items[0].Id);

        // removed rows are kept but inactive, so history is not lost
        var all = await db.LookupValues.Where(l => l.FieldName == "Civil Status").ToListAsync();
        Assert.Equal(4, all.Count);
        Assert.False(all.Single(l => l.Value == "Married").IsActive);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Blank_entries_are_rejected(string blank)
    {
        await using var db = _env.CreateDb();
        SignIn();

        var result = await new EntryService(db, _env.Session).SaveSingleLineAsync(EntryFields.Gender, Patients, Lines("Male", blank));

        Assert.True(result.IsFailure);
        Assert.Empty(await db.LookupValues.ToListAsync());
    }

    [Fact]
    public async Task Duplicates_are_rejected_ignoring_case_and_spaces()
    {
        await using var db = _env.CreateDb();
        SignIn();

        var result = await new EntryService(db, _env.Session).SaveSingleLineAsync(EntryFields.Gender, Patients, Lines("Male", " male "));

        Assert.True(result.IsFailure);
        Assert.Contains("only be listed once", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Entries_are_trimmed_and_over_long_ones_rejected()
    {
        await using var db = _env.CreateDb();
        SignIn();
        var service = new EntryService(db, _env.Session);

        var tooLong = await service.SaveSingleLineAsync(EntryFields.Gender, Patients, Lines(new string('x', EntryService.ValueMaxLength + 1)));
        var trimmed = await service.SaveSingleLineAsync(EntryFields.Gender, Patients, Lines("  Male  "));

        Assert.True(tooLong.IsFailure);
        Assert.Equal("Male", trimmed.Value.Items.Single().Value);
    }

    [Fact]
    public async Task A_module_list_is_separate_from_the_same_field_name_in_another_module_and_from_general_lists()
    {
        await using var db = _env.CreateDb();
        _env.Session.SignIn(new AuthenticatedUser(
            3, "u", "U", false, false,
            [new ModulePermission(8, true, true, true, false), new ModulePermission(Serology, true, true, true, false)]));
        var service = new EntryService(db, _env.Session);

        await service.SaveSingleLineAsync(SerologyTest, Serology, Lines("Hepatitis B"));
        await service.SaveSingleLineAsync(ImmunologyTest, 8, Lines("Glycated Hemoglobin", "CRP"));
        await service.SaveSingleLineAsync(new EntryField("Test", EntryKind.SingleLine), 8, Lines("General test"));

        Assert.Equal(["Hepatitis B"], await service.GetChoicesAsync(SerologyTest));
        Assert.Equal(["Glycated Hemoglobin", "CRP"], await service.GetChoicesAsync(ImmunologyTest));
        Assert.Equal(["General test"], await service.GetChoicesAsync(new EntryField("Test", EntryKind.SingleLine)));
    }

    [Fact]
    public async Task The_scope_is_named_for_the_header_of_the_builder()
    {
        await using var db = _env.CreateDb();
        db.Modules.Add(new Module { Id = Serology, ModuleName = "Serology", ModuleType = new ModuleType { Id = 3 } });
        await db.SaveChangesAsync();
        _env.Session.SignIn(new AuthenticatedUser(1, "a", "A", true, false, []));
        var service = new EntryService(db, _env.Session);

        var general = (await service.GetSingleLineAsync(EntryFields.Gender, Patients)).Value;
        var module = (await service.GetSingleLineAsync(SerologyTest, Serology)).Value;

        Assert.StartsWith("General list", general.ScopeName, StringComparison.Ordinal);
        Assert.Equal("Serology", module.ScopeName);
        Assert.Equal("Test", module.FieldName);
    }

    [Fact]
    public async Task Viewing_needs_access_to_the_host_screen_and_saving_needs_edit_rights()
    {
        await using var db = _env.CreateDb();
        var service = new EntryService(db, _env.Session);

        SignIn(canEdit: false);
        Assert.True((await service.GetSingleLineAsync(EntryFields.Gender, Patients)).IsSuccess);
        Assert.Equal("Auth.Forbidden", (await service.SaveSingleLineAsync(EntryFields.Gender, Patients, Lines("Male"))).Error.Code);

        SignIn(canEdit: true, moduleId: 99);
        Assert.Equal("Auth.Forbidden", (await service.GetSingleLineAsync(EntryFields.Gender, Patients)).Error.Code);
    }

    [Fact]
    public async Task Asking_for_the_wrong_kind_of_list_is_an_error()
    {
        await using var db = _env.CreateDb();
        SignIn();
        var service = new EntryService(db, _env.Session);

        Assert.Equal("Entries.WrongKind", (await service.GetMultiLineAsync(EntryFields.Gender, Patients)).Error.Code);
        Assert.Equal("Entries.WrongKind", (await service.GetSingleLineAsync(HematologyResult, Patients)).Error.Code);
    }

    // ---------------------------------------------------------------- multi-line templates

    [Fact]
    public async Task Multi_line_templates_save_with_a_title_and_text_in_the_order_added()
    {
        await using var db = _env.CreateDb();
        SignIn(moduleId: 7);
        var service = new EntryService(db, _env.Session);

        var saved = await service.SaveMultiLineAsync(HematologyResult, 7,
        [
            new MultiLineEntry(0, "Normal", "All values are within range."),
            new MultiLineEntry(0, "Anemia", "Low hemoglobin.\r\nRepeat in two weeks."),
        ]);

        Assert.Equal(["Normal", "Anemia"], saved.Value.Items.Select(i => i.Title));
        Assert.Equal("Low hemoglobin.\r\nRepeat in two weeks.", saved.Value.Items[1].Text);
    }

    [Fact]
    public async Task Multi_line_titles_are_required_and_unique_but_text_may_be_empty()
    {
        await using var db = _env.CreateDb();
        SignIn(moduleId: 7);
        var service = new EntryService(db, _env.Session);

        Assert.True((await service.SaveMultiLineAsync(HematologyResult, 7, [new MultiLineEntry(0, " ", "text")])).IsFailure);
        Assert.True((await service.SaveMultiLineAsync(HematologyResult, 7,
            [new MultiLineEntry(0, "A", "x"), new MultiLineEntry(0, "a", "y")])).IsFailure);
        Assert.True((await service.SaveMultiLineAsync(HematologyResult, 7, [new MultiLineEntry(0, "Blank template", "")])).IsSuccess);
    }

    [Fact]
    public async Task Editing_a_template_keeps_its_id_and_removing_one_switches_it_off()
    {
        await using var db = _env.CreateDb();
        SignIn(moduleId: 7);
        var service = new EntryService(db, _env.Session);
        var saved = (await service.SaveMultiLineAsync(HematologyResult, 7,
            [new MultiLineEntry(0, "Normal", "ok"), new MultiLineEntry(0, "Abnormal", "bad")])).Value;

        var result = (await service.SaveMultiLineAsync(HematologyResult, 7, [saved.Items[0] with { Text = "all ok" }])).Value;

        var only = Assert.Single(result.Items);
        Assert.Equal(saved.Items[0].Id, only.Id);
        Assert.Equal("all ok", only.Text);
        Assert.Equal(1, await db.LookupValues.CountAsync(l => l.IsActive));
    }
}
