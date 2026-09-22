using System.Globalization;
using System.Windows;
using System.Windows.Media;
using CertSentry.Enums;
using CertSentry.Views.Converters;
using FluentAssertions;
using Xunit;

namespace CertSentry.Tests;

public class ConverterTests
{
    [Fact]
    public void HealthStatusToBrushConverter_ShouldReturnAppropriateColors()
    {
        var converter = new HealthStatusToBrushConverter();

        var validBrush = (SolidColorBrush)converter.Convert(CertificateHealthStatus.Valid, typeof(Brush), null, CultureInfo.InvariantCulture);
        validBrush.Color.G.Should().BeGreaterThan(validBrush.Color.R); // Green dominance

        var expiredBrush = (SolidColorBrush)converter.Convert(CertificateHealthStatus.Expired, typeof(Brush), null, CultureInfo.InvariantCulture);
        expiredBrush.Color.R.Should().BeGreaterThan(expiredBrush.Color.G); // Red dominance

        var expiringBrush = (SolidColorBrush)converter.Convert(CertificateHealthStatus.ExpiringSoon, typeof(Brush), null, CultureInfo.InvariantCulture);
        expiringBrush.Color.R.Should().BeGreaterThan(0); // Amber

        var errBrush = (SolidColorBrush)converter.Convert(ProbeSeverity.Error, typeof(Brush), null, CultureInfo.InvariantCulture);
        errBrush.Color.R.Should().BeGreaterThan(errBrush.Color.G);

        var httpsBrush = (SolidColorBrush)converter.Convert(PortServiceType.Https, typeof(Brush), null, CultureInfo.InvariantCulture);
        httpsBrush.Color.G.Should().BeGreaterThan(httpsBrush.Color.R);
    }

    [Theory]
    [InlineData(95, 0x10, 0x7C, 0x41)] // >= 85: Green
    [InlineData(70, 0xD8, 0x9B, 0x00)] // 60-84: Amber
    [InlineData(40, 0xD1, 0x34, 0x38)] // < 60: Red
    public void HealthScoreToBrushConverter_ShouldMatchScoreTiers(int score, byte r, byte g, byte b)
    {
        var converter = new HealthScoreToBrushConverter();
        var brush = (SolidColorBrush)converter.Convert(score, typeof(Brush), null, CultureInfo.InvariantCulture);

        brush.Color.R.Should().Be(r);
        brush.Color.G.Should().Be(g);
        brush.Color.B.Should().Be(b);
    }

    [Fact]
    public void NullToVisibilityConverter_ShouldHandleNullAndInversion()
    {
        var converter = new NullToVisibilityConverter { Invert = false };
        converter.Convert(null, typeof(Visibility), null, CultureInfo.InvariantCulture).Should().Be(Visibility.Collapsed);
        converter.Convert("NotNull", typeof(Visibility), null, CultureInfo.InvariantCulture).Should().Be(Visibility.Visible);

        var invertedConverter = new NullToVisibilityConverter { Invert = true };
        invertedConverter.Convert(null, typeof(Visibility), null, CultureInfo.InvariantCulture).Should().Be(Visibility.Visible);
        invertedConverter.Convert("NotNull", typeof(Visibility), null, CultureInfo.InvariantCulture).Should().Be(Visibility.Collapsed);

        // Test parameter-based inversion
        converter.Convert(null, typeof(Visibility), "Inverse", CultureInfo.InvariantCulture).Should().Be(Visibility.Visible);
        converter.Convert("NotNull", typeof(Visibility), "Inverse", CultureInfo.InvariantCulture).Should().Be(Visibility.Collapsed);
    }

    [Fact]
    public void BoolToVisibilityConverter_ShouldHandleBooleanAndInversion()
    {
        var converter = new BoolToVisibilityConverter { Invert = false };
        converter.Convert(true, typeof(Visibility), null, CultureInfo.InvariantCulture).Should().Be(Visibility.Visible);
        converter.Convert(false, typeof(Visibility), null, CultureInfo.InvariantCulture).Should().Be(Visibility.Collapsed);

        var invertedConverter = new BoolToVisibilityConverter { Invert = true };
        invertedConverter.Convert(true, typeof(Visibility), null, CultureInfo.InvariantCulture).Should().Be(Visibility.Collapsed);
        invertedConverter.Convert(false, typeof(Visibility), null, CultureInfo.InvariantCulture).Should().Be(Visibility.Visible);

        // Test parameter-based inversion
        converter.Convert(true, typeof(Visibility), "Inverse", CultureInfo.InvariantCulture).Should().Be(Visibility.Collapsed);
        converter.Convert(false, typeof(Visibility), "Inverse", CultureInfo.InvariantCulture).Should().Be(Visibility.Visible);
    }

    [Fact]
    public void StringNotEmptyToVisibilityConverter_ShouldHandleEmptyAndNonEmptyStrings()
    {
        var converter = new StringNotEmptyToVisibilityConverter();
        converter.Convert(null, typeof(Visibility), null, CultureInfo.InvariantCulture).Should().Be(Visibility.Collapsed);
        converter.Convert("", typeof(Visibility), null, CultureInfo.InvariantCulture).Should().Be(Visibility.Collapsed);
        converter.Convert("   ", typeof(Visibility), null, CultureInfo.InvariantCulture).Should().Be(Visibility.Collapsed);
        converter.Convert("Valid text", typeof(Visibility), null, CultureInfo.InvariantCulture).Should().Be(Visibility.Visible);
    }
}
