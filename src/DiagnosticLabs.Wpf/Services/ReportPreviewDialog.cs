using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using DiagnosticLabs.Application.LabResults;
using DiagnosticLabs.Wpf.Printing;

namespace DiagnosticLabs.Wpf.Services;

/// <summary>Shows a report on a Letter page. The viewer has the printer picker (including "Microsoft Print to PDF" for a PDF) and zoom.</summary>
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
            Content = viewer,
            Width = 940,
            Height = 780,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = System.Windows.Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                    ?? System.Windows.Application.Current.MainWindow,
        };

        window.ShowDialog();
    }
}
