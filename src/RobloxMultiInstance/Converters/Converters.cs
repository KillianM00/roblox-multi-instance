using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

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
    public object ConvertBack(object value, Type t, object? p, CultureInfo c) => throw new NotImplementedException();
}

public class BoolToMutexTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
        => (value is bool x && x) ? "MUTEX ACTIVE" : "MUTEX INACTIVE";
    public object ConvertBack(object value, Type t, object? p, CultureInfo c) => throw new NotImplementedException();
}

public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
        => !(value is bool b && b);
    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => !(value is bool b && b);
}

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
        => (value is bool b && b) ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object value, Type t, object? p, CultureInfo c) => throw new NotImplementedException();
}

public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
        => (value is bool b && b) ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object value, Type t, object? p, CultureInfo c) => throw new NotImplementedException();
}

public class IntToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
        => (value is int n && n > 0) ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object value, Type t, object? p, CultureInfo c) => throw new NotImplementedException();
}

public class IntToInverseVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
        => (value is int n && n > 0) ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object value, Type t, object? p, CultureInfo c) => throw new NotImplementedException();
}

public class StringToBitmapConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string url || string.IsNullOrWhiteSpace(url)) return null;
        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            bmp.UriSource = new Uri(url, UriKind.Absolute);
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch
        {
            return null;
        }
    }
    public object ConvertBack(object value, Type t, object? p, CultureInfo c) => throw new NotImplementedException();
}

public class TabSelectedToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
    {
        var active = value?.ToString() == parameter?.ToString();
        var key = active ? "AccentBrush" : "PanelAltBrush";
        return Application.Current.FindResource(key) as Brush ?? Brushes.Gray;
    }
    public object ConvertBack(object value, Type t, object? p, CultureInfo c) => throw new NotImplementedException();
}

public class TabSelectedToForegroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
    {
        var active = value?.ToString() == parameter?.ToString();
        return active ? Brushes.White : (Application.Current.FindResource("MutedTextBrush") as Brush ?? Brushes.Gray);
    }
    public object ConvertBack(object value, Type t, object? p, CultureInfo c) => throw new NotImplementedException();
}

public class TabVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
        => value?.ToString() == parameter?.ToString() ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object value, Type t, object? p, CultureInfo c) => throw new NotImplementedException();
}
