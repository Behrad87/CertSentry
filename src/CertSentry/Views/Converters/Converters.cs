using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using CertSentry.Enums;

namespace CertSentry.Views.Converters;

public class HealthStatusToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush GreenBrush = new(Color.FromRgb(0x10, 0x7C, 0x41));  // Fluent Success Green
    private static readonly SolidColorBrush YellowBrush = new(Color.FromRgb(0xD8, 0x9B, 0x00)); // Fluent Warning Amber
    private static readonly SolidColorBrush RedBrush = new(Color.FromRgb(0xD1, 0x34, 0x38));    // Fluent Error Red
    private static readonly SolidColorBrush GrayBrush = new(Color.FromRgb(0x60, 0x60, 0x60));   // Neutral Gray

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is CertificateHealthStatus status)
        {
            return status switch
            {
                CertificateHealthStatus.Valid => GreenBrush,
                CertificateHealthStatus.ExpiringSoon => YellowBrush,
                CertificateHealthStatus.Warning => YellowBrush,
                CertificateHealthStatus.Expired => RedBrush,
                CertificateHealthStatus.Untrusted => RedBrush,
                _ => GrayBrush
            };
        }

        if (value is ProbeSeverity severity)
        {
            return severity switch
            {
                ProbeSeverity.Success => GreenBrush,
                ProbeSeverity.Information => new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD4)),
                ProbeSeverity.Warning => YellowBrush,
                ProbeSeverity.Error => RedBrush,
                _ => GrayBrush
            };
        }

        if (value is PortServiceType portStatus)
        {
            return portStatus switch
            {
                PortServiceType.Https => GreenBrush,
                PortServiceType.Http => YellowBrush,
                _ => GrayBrush
            };
        }

        return GrayBrush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class HealthScoreToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int score)
        {
            if (score >= 85)
                return new SolidColorBrush(Color.FromRgb(0x10, 0x7C, 0x41));
            if (score >= 60)
                return new SolidColorBrush(Color.FromRgb(0xD8, 0x9B, 0x00));
            return new SolidColorBrush(Color.FromRgb(0xD1, 0x34, 0x38));
        }
        return Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class NullToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isNull = value == null;
        bool shouldInvert = Invert || string.Equals(parameter as string, "Inverse", StringComparison.OrdinalIgnoreCase) || string.Equals(parameter as string, "Invert", StringComparison.OrdinalIgnoreCase);
        if (shouldInvert) isNull = !isNull;
        return isNull ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class BoolToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b)
        {
            bool shouldInvert = Invert || string.Equals(parameter as string, "Inverse", StringComparison.OrdinalIgnoreCase) || string.Equals(parameter as string, "Invert", StringComparison.OrdinalIgnoreCase);
            if (shouldInvert) b = !b;
            return b ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class StringNotEmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return !string.IsNullOrWhiteSpace(value as string) ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}
