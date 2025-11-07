using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Netswitch.UI.Converters;

public sealed class BooleanToBrushConverter : IValueConverter
{
    public Brush TrueBrush { get; set; } = TryFindBrush("AccentBrush");

    public Brush FalseBrush { get; set; } = TryFindBrush("ErrorBrush");

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool flag)
        {
            return flag ? TrueBrush : FalseBrush;
        }

        return Brushes.Transparent;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static SolidColorBrush TryFindBrush(string key)
        => Application.Current.Resources[key] as SolidColorBrush ?? Brushes.Transparent;
}
