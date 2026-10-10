using System.Windows;
using System.Windows.Controls;

namespace DiagnosticLabs.Wpf.Controls;

public partial class LabResultBottom : UserControl
{
    /// <summary>False for a form that has no Remarks (Clinical Chemistry): only the signatories are shown.</summary>
    public static readonly DependencyProperty ShowRemarksProperty = DependencyProperty.Register(
        nameof(ShowRemarks), typeof(bool), typeof(LabResultBottom),
        new PropertyMetadata(true, (d, e) => ((LabResultBottom)d).RemarksPanel.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed));

    /// <summary>True for a form signed by a second medical technologist (it binds to <c>MedicalTechnologist2</c> on the screen).</summary>
    public static readonly DependencyProperty ShowSecondTechnologistProperty = DependencyProperty.Register(
        nameof(ShowSecondTechnologist), typeof(bool), typeof(LabResultBottom),
        new PropertyMetadata(false, (d, e) => ((LabResultBottom)d).ApplySecondTechnologist((bool)e.NewValue)));

    public LabResultBottom() => InitializeComponent();

    public bool ShowRemarks
    {
        get => (bool)GetValue(ShowRemarksProperty);
        set => SetValue(ShowRemarksProperty, value);
    }

    public bool ShowSecondTechnologist
    {
        get => (bool)GetValue(ShowSecondTechnologistProperty);
        set => SetValue(ShowSecondTechnologistProperty, value);
    }

    private void ApplySecondTechnologist(bool show)
    {
        SecondPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        SecondGap.Width = show ? new GridLength(40) : new GridLength(0);
        SecondColumn.Width = show ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
    }
}
