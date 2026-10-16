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

    /// <summary>True for a form that prints the licence numbers of its signatories: a box for each appears under the names.</summary>
    public static readonly DependencyProperty ShowLicensesProperty = DependencyProperty.Register(
        nameof(ShowLicenses), typeof(bool), typeof(LabResultBottom),
        new PropertyMetadata(false, (d, e) => ((LabResultBottom)d).ApplyLicenses((bool)e.NewValue)));

    /// <summary>False for a form without a first medical technologist (a custom form that is signed only by the pathologist, say).</summary>
    public static readonly DependencyProperty ShowFirstTechnologistProperty = DependencyProperty.Register(
        nameof(ShowFirstTechnologist), typeof(bool), typeof(LabResultBottom),
        new PropertyMetadata(true, (d, e) => ((LabResultBottom)d).FirstPanel.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed));

    /// <summary>False for a form without a pathologist.</summary>
    public static readonly DependencyProperty ShowPathologistProperty = DependencyProperty.Register(
        nameof(ShowPathologist), typeof(bool), typeof(LabResultBottom),
        new PropertyMetadata(true, (d, e) => ((LabResultBottom)d).PathologistPanel.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed));

    /// <summary>False for a form with no signatories at all: the whole row is left out.</summary>
    public static readonly DependencyProperty ShowSignatoriesProperty = DependencyProperty.Register(
        nameof(ShowSignatories), typeof(bool), typeof(LabResultBottom),
        new PropertyMetadata(true, (d, e) => ((LabResultBottom)d).SignatureGrid.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed));

    /// <summary>The space between the Remarks box and the signatories (a form with a long table gives them more room).</summary>
    public static readonly DependencyProperty SignatoriesSpacingProperty = DependencyProperty.Register(
        nameof(SignatoriesSpacing), typeof(double), typeof(LabResultBottom),
        new PropertyMetadata(6.0, (d, e) => ((LabResultBottom)d).SignatureGrid.Margin = new Thickness(0, (double)e.NewValue, 0, 0)));

    public LabResultBottom() => InitializeComponent();

    public double SignatoriesSpacing
    {
        get => (double)GetValue(SignatoriesSpacingProperty);
        set => SetValue(SignatoriesSpacingProperty, value);
    }

    public bool ShowFirstTechnologist
    {
        get => (bool)GetValue(ShowFirstTechnologistProperty);
        set => SetValue(ShowFirstTechnologistProperty, value);
    }

    public bool ShowPathologist
    {
        get => (bool)GetValue(ShowPathologistProperty);
        set => SetValue(ShowPathologistProperty, value);
    }

    public bool ShowSignatories
    {
        get => (bool)GetValue(ShowSignatoriesProperty);
        set => SetValue(ShowSignatoriesProperty, value);
    }

    public bool ShowRemarks
    {
        get => (bool)GetValue(ShowRemarksProperty);
        set => SetValue(ShowRemarksProperty, value);
    }

    public bool ShowLicenses
    {
        get => (bool)GetValue(ShowLicensesProperty);
        set => SetValue(ShowLicensesProperty, value);
    }

    public bool ShowSecondTechnologist
    {
        get => (bool)GetValue(ShowSecondTechnologistProperty);
        set => SetValue(ShowSecondTechnologistProperty, value);
    }

    private void ApplyLicenses(bool show)
    {
        var visibility = show ? Visibility.Visible : Visibility.Collapsed;
        LicenseFirst.Visibility = visibility;
        LicenseSecond.Visibility = visibility;
        LicensePathologist.Visibility = visibility;
    }

    private void ApplySecondTechnologist(bool show)
    {
        SecondPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        SecondGap.Width = show ? new GridLength(40) : new GridLength(0);
        SecondColumn.Width = show ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
    }
}
