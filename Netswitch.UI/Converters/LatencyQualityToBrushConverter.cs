using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Netswitch.Core.Models;

namespace Netswitch.UI.Converters;

public sealed class LatencyQualityToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not LatencyQuality quality)
        {
            return Brushes.Transparent;
        }

        return quality switch
        {
            LatencyQuality.Green => TryFindBrush("AccentBrush"),
            LatencyQuality.Yellow => TryFindBrush("WarningBrush"),
            LatencyQuality.Red => TryFindBrush("ErrorBrush"),
            _ => Brushes.Transparent
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static Brush TryFindBrush(string key)
        => Application.Current.Resources[key] as Brush ?? Brushes.Transparent;
}
