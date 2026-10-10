using System.Windows;
using System.Windows.Media;

namespace DiagnosticLabs.Wpf.Controls;

/// <summary>
/// Optional icon for any button: <c>icn:ButtonIcon.Data="{StaticResource Icon.Save}"</c>. The button template draws it in the
/// button's text colour next to the label; <see cref="AtEndProperty"/> moves it to the right of the label.
/// </summary>
public static class ButtonIcon
{
    public static readonly DependencyProperty DataProperty =
        DependencyProperty.RegisterAttached("Data", typeof(Geometry), typeof(ButtonIcon), new FrameworkPropertyMetadata(null));

    public static readonly DependencyProperty AtEndProperty =
        DependencyProperty.RegisterAttached("AtEnd", typeof(bool), typeof(ButtonIcon), new FrameworkPropertyMetadata(false));

    public static Geometry? GetData(DependencyObject element) => (Geometry?)element.GetValue(DataProperty);
    public static void SetData(DependencyObject element, Geometry? value) => element.SetValue(DataProperty, value);

    public static bool GetAtEnd(DependencyObject element) => (bool)element.GetValue(AtEndProperty);
    public static void SetAtEnd(DependencyObject element, bool value) => element.SetValue(AtEndProperty, value);
}
