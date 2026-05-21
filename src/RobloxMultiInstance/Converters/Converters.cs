using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace RobloxMultiInstance.Converters;

public class BoolToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
    {
        var b = value is bool x && x;
        return b
            ? Application.Current.FindResource("SuccessBrush") as Brush ?? Brushes.Green
            : Application.Current.FindResource("DangerBrush") as Brush ?? Brushes.Red;
    }

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class BoolToMutexTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
    {
        return (value is bool x && x) ? "MUTEX ACTIVE" : "MUTEX INACTIVE";
    }

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
    {
        return !(value is bool b && b);
    }

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
    {
        return !(value is bool b && b);
    }
}
