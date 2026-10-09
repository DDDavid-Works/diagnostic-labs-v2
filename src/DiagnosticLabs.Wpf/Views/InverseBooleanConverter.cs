using System.Globalization;
using System.Windows.Data;

namespace DiagnosticLabs.Wpf.Views;

/// <summary>Flips a bool, so two radio buttons can share one bound property (one bound directly, one through this).</summary>
public sealed class InverseBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is not true;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => value is not true;
}
