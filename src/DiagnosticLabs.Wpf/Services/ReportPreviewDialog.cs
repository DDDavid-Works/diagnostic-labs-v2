using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using DiagnosticLabs.Application.LabResults;
using DiagnosticLabs.Wpf.Printing;
using Microsoft.Win32;

namespace DiagnosticLabs.Wpf.Services;

/// <summary>Shows a report on a Letter page. The viewer has the printer picker and zoom; the button above it saves the report as a PDF.</summary>
public interface IReportPreviewDialog
{
    void Show(PrintableReport report);
}

public sealed class ReportPreviewDialog : IReportPreviewDialog
{
    public void Show(PrintableReport report)
    {
        var document = ReportDocumentBuilder.Build(report);
        var viewer = new DocumentViewer { Document = document };

        var window = new Window
        {
            Title = $"Print preview - {report.Title}",
            Width = 940,
            Height = 780,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = System.Windows.Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                    ?? System.Windows.Application.Current.MainWindow,
        };

        var export = new Button { Content = "Save as PDF...", Padding = new Thickness(12, 5, 12, 5), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(8) };
        export.Click += (_, _) => ExportPdf(window, document, report);

        var layout = new DockPanel();
        DockPanel.SetDock(export, Dock.Top);
        layout.Children.Add(export);
        layout.Children.Add(viewer);
        window.Content = layout;

        window.ShowDialog();
    }

    private static void ExportPdf(Window owner, FixedDocument document, PrintableReport report)
    {
        var patient = report.PatientLines.FirstOrDefault(l => l.Label.StartsWith("Name", StringComparison.OrdinalIgnoreCase))?.Value;
        var name = string.Join("-", new[] { report.Title, patient }.Where(s => !string.IsNullOrWhiteSpace(s)));
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
