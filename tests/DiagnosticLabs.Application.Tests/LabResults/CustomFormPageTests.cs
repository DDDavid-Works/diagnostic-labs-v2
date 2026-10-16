using DiagnosticLabs.Application.LabResults.CustomForms;

namespace DiagnosticLabs.Application.Tests.LabResults;

/// <summary>The one-page rule of a custom form: the same numbers the page is laid out with decide whether a design fits.</summary>
public class CustomFormPageTests
{
    private static GroupDefinition Group(int rows, string? title = null, bool showReference = true) =>
        new(Guid.NewGuid(), title, showReference, [.. Enumerable.Range(1, rows).Select(i => new RowDefinition(Guid.NewGuid(), RowKind.Test, $"Test {i}"))]);

    private static FormDefinition Design(int groups = 1, int rows = 10, int textboxes = 0, bool remarks = true, int signatories = 2, bool licenses = true, string? title = null) =>
        FormDefinition.NewForm() with
        {
            Footer = new FooterDefinition(
                [.. Enumerable.Range(0, signatories).Select(i => new SignatorySlot(i < 2 ? SignatoryRole.MedicalTechnologist : SignatoryRole.Pathologist))], licenses, remarks),
            Body = new BodyDefinition([.. Enumerable.Range(0, groups).Select(_ => Group(rows, title))], [.. Enumerable.Range(0, textboxes).Select(i => new TextboxDefinition(Guid.NewGuid(), $"Box {i}"))]),
        };

    [Fact]
    public void A_typical_form_fits_and_the_largest_one_does_not()
    {
        Assert.True(CustomFormPage.FitsOnOnePage(Design(groups: 1, rows: 20, textboxes: 1)));
        Assert.True(CustomFormPage.FitsOnOnePage(Design(groups: 2, rows: 15, textboxes: 1)));
        Assert.False(CustomFormPage.FitsOnOnePage(Design(groups: 2, rows: BodyDefinition.MaxRowsPerGroup, textboxes: BodyDefinition.MaxTextboxes)));
        Assert.True(CustomFormPage.Overflow(Design(groups: 2, rows: BodyDefinition.MaxRowsPerGroup, textboxes: BodyDefinition.MaxTextboxes)) > 100);
        Assert.Equal(0, CustomFormPage.Overflow(Design(rows: 5)));
    }

    [Fact]
    public void How_many_rows_one_group_can_have_depends_on_what_else_is_on_the_page()
    {
        // with Remarks and two signatories with licence numbers 28 rows fit and the 29th pushes the note off the page
        Assert.True(CustomFormPage.FitsOnOnePage(Design(rows: 28)));
        Assert.False(CustomFormPage.FitsOnOnePage(Design(rows: 29)));

        // without Remarks, licence lines and signatories there is room for all 30
        Assert.True(CustomFormPage.FitsOnOnePage(Design(rows: BodyDefinition.MaxRowsPerGroup, remarks: false, signatories: 0)));
        Assert.False(CustomFormPage.FitsOnOnePage(Design(rows: BodyDefinition.MaxRowsPerGroup, textboxes: 3)));
    }

    [Fact]
    public void Every_row_text_box_and_title_takes_space_and_a_longer_group_decides_the_height()
    {
        var baseline = CustomFormPage.EstimateBottom(Design(rows: 10));

        Assert.Equal(baseline + CustomFormPage.RowHeight, CustomFormPage.EstimateBottom(Design(rows: 11)), 6);
        Assert.Equal(baseline + CustomFormPage.TextBlockHeight, CustomFormPage.EstimateBottom(Design(rows: 10, textboxes: 1)), 6);
        Assert.Equal(baseline - CustomFormPage.TextBlockHeight, CustomFormPage.EstimateBottom(Design(rows: 10, remarks: false)), 6);
        Assert.Equal(baseline + CustomFormPage.TitleRowHeight, CustomFormPage.EstimateBottom(Design(rows: 10, title: "TITLE")), 6);

        // two groups share the height of the longer one, with taller (two line) captions
        var uneven = FormDefinition.NewForm() with { Body = new BodyDefinition([Group(5), Group(12)], []) };
        var even = FormDefinition.NewForm() with { Body = new BodyDefinition([Group(12), Group(12)], []) };
        Assert.Equal(CustomFormPage.EstimateBottom(even), CustomFormPage.EstimateBottom(uneven), 6);
        Assert.Equal(CustomFormPage.CaptionHeight(1) + 13, CustomFormPage.CaptionHeight(2), 6);
    }

    [Fact]
    public void The_signatories_and_their_licence_lines_take_the_space_the_page_gives_them()
    {
        var none = CustomFormPage.EstimateBottom(Design(signatories: 0));
        var two = CustomFormPage.EstimateBottom(Design(signatories: 2, licenses: false));
        var twoWithLicenses = CustomFormPage.EstimateBottom(Design(signatories: 2, licenses: true));

        Assert.Equal(CustomFormPage.NoteOffsetWithSignatories - CustomFormPage.NoteOffsetWithoutSignatories, two - none, 6);
        Assert.Equal(CustomFormPage.NoteOffsetWithLicenses - CustomFormPage.NoteOffsetWithSignatories, twoWithLicenses - two, 6);
        Assert.Equal(twoWithLicenses, CustomFormPage.EstimateBottom(Design(signatories: 3, licenses: true)), 6); // three sit side by side, so no taller
    }

    [Fact]
    public void A_form_without_groups_has_no_table_and_the_empty_form_fits_easily()
    {
        var empty = FormDefinition.NewForm() with { Body = BodyDefinition.Empty };

        Assert.Equal(0, CustomFormPage.TableHeight(empty.Body));
        Assert.True(CustomFormPage.FitsOnOnePage(empty));
        Assert.True(CustomFormPage.EstimateBottom(empty) > CustomFormPage.Top);
    }

    [Fact]
    public void The_limits_sit_inside_the_page()
    {
        Assert.True(CustomFormPage.BottomLimit < CustomFormPage.PageHeight);
        Assert.True(CustomFormPage.Left < CustomFormPage.Right && CustomFormPage.Right < 816);
        Assert.True(CustomFormPage.Top > 236.3); // under the title bar
    }
}
