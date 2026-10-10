using System.Windows;
using System.Windows.Controls;

namespace DiagnosticLabs.Wpf.Controls;

public partial class LabResultBottom : UserControl
{
    /// <summary>False for a form that has no Remarks (Clinical Chemistry): only the signatories are shown.</summary>
    public static readonly DependencyProperty ShowRemarksProperty = DependencyProperty.Register(
        nameof(ShowRemarks), typeof(bool), typeof(LabResultBottom),
        new PropertyMetadata(true, (d, e) => ((LabResultBottom)d).RemarksPanel.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed));

    public LabResultBottom() => InitializeComponent();

    public bool ShowRemarks
    {
        get => (bool)GetValue(ShowRemarksProperty);
        set => SetValue(ShowRemarksProperty, value);
    }
}
