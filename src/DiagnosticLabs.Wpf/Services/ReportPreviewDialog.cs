using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using DiagnosticLabs.Application.LabResults;
using DiagnosticLabs.Wpf.Printing;
using Microsoft.Win32;

namespace DiagnosticLabs.Wpf.Services;

/// <summary>Shows a report on a Letter page (half a Letter sheet for the Urinalysis and Fecalysis). The viewer has the printer picker and zoom; the button above it saves the report as a PDF.</summary>
public interface IReportPreviewDialog
{
    void Show(PrintableReport report);

    /// <summary>Several reports in one document, a page (or two) each, in the order given (Batch Print); <paramref name="fileName"/> is what the PDF is called.</summary>
    void Show(IReadOnlyList<PrintableReport> reports, string title, string fileName);

    /// <summary>Shows an already laid out document (a report that is not a result form, such as the Sales Report); <paramref name="fileName"/> is what the PDF is called.</summary>
    void Show(FixedDocument document, string title, string fileName);
}

public sealed class ReportPreviewDialog : IReportPreviewDialog
{
    public void Show(PrintableReport report)
    {
        var patient = report.PatientLines.FirstOrDefault(l => l.Label.StartsWith("Name", StringComparison.OrdinalIgnoreCase))?.Value;
        var fileName = string.Join("-", new[] { report.Title, patient }.Where(s => !string.IsNullOrWhiteSpace(s)));
        Show([report], report.Title, fileName);
    }

    /// <summary>
    /// When a signature is on file for someone who signs these reports, a "Print signatures" box (ticked) is shown above the preview;
    /// clearing it lays the reports out again without them, for printing, saving as PDF and the half-sheet printing alike.
    /// </summary>
    public void Show(IReadOnlyList<PrintableReport> reports, string title, string fileName)
    {
        if (!reports.Any(r => r.HasSignatures))
        {
            Show(ReportDocumentBuilder.Build(reports), title, fileName);
            return;
        }

        var unsigned = reports.Select(r => r.WithoutSignatures()).ToList();
        var signatures = new CheckBox
        {
            Content = "Print signatures",
            IsChecked = true,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 8, 0),
            ToolTip = "The signatures on file are printed above the names. Clear this to print the names without them (to sign by hand).",
        };
        Show(ReportDocumentBuilder.Build(reports), title, fileName, signatures, signed => ReportDocumentBuilder.Build(signed ? reports : unsigned));
    }

    public void Show(FixedDocument document, string title, string fileName) => Show(document, title, fileName, null, null);

    private static void Show(FixedDocument first, string title, string fileName, CheckBox? signatures, Func<bool, FixedDocument>? rebuild)
    {
        var document = first;
        var viewer = new DocumentViewer { Document = document };
        if (signatures is not null && rebuild is not null)
        {
            void Rebuild()
            {
                document = rebuild(signatures.IsChecked == true);
                viewer.Document = document;
            }

            signatures.Checked += (_, _) => Rebuild();
            signatures.Unchecked += (_, _) => Rebuild();
        }

        var window = new Window
        {
            Title = $"Print preview - {title}",
            Width = 940,
            Height = 780,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = System.Windows.Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                    ?? System.Windows.Application.Current.MainWindow,
        };

        var export = new Button { Content = "Save as PDF...", Padding = new Thickness(12, 5, 12, 5), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(8) };
        export.Click += (_, _) => ExportPdf(window, document, fileName);

        var bar = new StackPanel { Orientation = Orientation.Horizontal };
        bar.Children.Add(export);
        if (signatures is not null)
            bar.Children.Add(signatures);

        var layout = new DockPanel();
        DockPanel.SetDock(bar, Dock.Top);
        layout.Children.Add(bar);
        layout.Children.Add(viewer);
        window.Content = layout;

        // A form printed on half a Letter sheet: the viewer's Print asks the printer for Statement paper (5.5 x 8.5 in) turned on its side,
        // so the page comes out on half a sheet. The window sees the command before the viewer does.
        if (ReportDocumentBuilder.IsHalfLetter(document))
        {
            var print = new CommandBinding(ApplicationCommands.Print);
            print.PreviewExecuted += (_, e) =>
            {
                e.Handled = true;
                PrintHalfLetter(document, title);
            };
            window.CommandBindings.Add(print);
        }

        window.ShowDialog();
    }

    private static void PrintHalfLetter(FixedDocument document, string title)
    {
        var dialog = new PrintDialog();
        dialog.PrintTicket.PageMediaSize = new PageMediaSize(PageMediaSizeName.NorthAmericaStatement, ReportDocumentBuilder.HalfPageHeight, ReportDocumentBuilder.PageWidth);
        dialog.PrintTicket.PageOrientation = PageOrientation.Landscape;
        if (dialog.ShowDialog() == true)
            dialog.PrintDocument(document.DocumentPaginator, title);
    }

    private static void ExportPdf(Window owner, FixedDocument document, string name)
    {
        foreach (var bad in System.IO.Path.GetInvalidFileNameChars())
            name = name.Replace(bad, '_');

        var dialog = new SaveFileDialog
        {
            Title = "Save as PDF",
            Filter = "PDF file (*.pdf)|*.pdf",
            FileName = name + ".pdf",
            AddExtension = true,
            DefaultExt = ".pdf",
        };
        if (dialog.ShowDialog(owner) != true)
            return;

        try
        {
            PdfExporter.Save(document, dialog.FileName);
            MessageBox.Show(owner, $"Saved to {dialog.FileName}", "Save as PDF", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(owner, $"The PDF could not be saved.\n\n{ex.Message}", "Save as PDF", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
