using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Netswitch.Core.Models;

namespace Netswitch.UI.Converters;

public sealed class AlertSeverityToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not AlertSeverity severity)
        {
            return new SolidColorBrush(Colors.Gray);
        }

        return severity switch
        {
            AlertSeverity.Critical => new SolidColorBrush(Color.FromRgb(220, 38, 38)),
            AlertSeverity.Error => new SolidColorBrush(Color.FromRgb(239, 68, 68)),
            AlertSeverity.Warning => new SolidColorBrush(Color.FromRgb(251, 146, 60)),
            AlertSeverity.Info => new SolidColorBrush(Color.FromRgb(59, 130, 246)),
            _ => new SolidColorBrush(Colors.Gray)
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
