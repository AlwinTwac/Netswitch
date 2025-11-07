using System;
using System.Globalization;
using System.Windows.Data;

namespace Netswitch.UI.Converters;

public sealed class BytesToReadableConverter : IValueConverter
{
    public bool ShowPerSecond { get; set; }

    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null)
        {
            return "--";
        }

        double bytes = value switch
        {
            double d => d,
            float f => f,
            long l => l,
            int i => i,
            _ => 0d
        };

        if (double.IsNaN(bytes) || double.IsInfinity(bytes))
        {
            return "--";
        }

        var absolute = Math.Abs(bytes);
        var units = new[] { "B", "KB", "MB", "GB", "TB" };
        var order = 0;
        while (absolute >= 1024 && order < units.Length - 1)
        {
            absolute /= 1024;
            order++;
        }

        var scaled = absolute;
        var sign = Math.Sign(bytes);
        scaled *= sign;

        var suffix = units[order];
        if (ShowPerSecond)
        {
            suffix += "/s";
        }

        return scaled switch
        {
            >= 100 => $"{scaled:0} {suffix}",
            >= 10 => $"{scaled:0.0} {suffix}",
            _ => $"{scaled:0.00} {suffix}"
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
