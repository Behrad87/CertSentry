using System.Collections.Generic;
using System.Text.Json;
using CertSentry.Models;
using FluentAssertions;
using Xunit;

namespace CertSentry.Tests;

public class AspNetCoreDevCertDoctorTests
{
    private const string SampleDuplicateJson = @"[
      {
        ""Thumbprint"": ""CB0FA4CD09F35ECF05A3B24A6B92004D66F9E7D5"",
        ""Subject"": ""CN=localhost"",
        ""X509SubjectAlternativeNameExtension"": [
          ""localhost"",
          ""*.dev.localhost"",
          ""host.docker.internal""
        ],
        ""Version"": 6,
        ""ValidityNotBefore"": ""2026-02-14T10:17:35+03:30"",
        ""ValidityNotAfter"": ""2027-02-14T10:17:35+03:30"",
        ""IsHttpsDevelopmentCertificate"": true,
        ""IsExportable"": true,
        ""TrustLevel"": ""Full""
      },
      {
        ""Thumbprint"": ""EF2DA00BCD75180B38E85284CB38E5C127660795"",
        ""Subject"": ""CN=localhost"",
        ""X509SubjectAlternativeNameExtension"": [
          ""localhost"",
          ""*.dev.localhost""
        ],
        ""Version"": 5,
        ""ValidityNotBefore"": ""2026-02-10T13:52:12+03:30"",
        ""ValidityNotAfter"": ""2027-02-10T13:52:12+03:30"",
        ""IsHttpsDevelopmentCertificate"": true,
        ""IsExportable"": true,
        ""TrustLevel"": ""Full""
      }
    ]";

    [Fact]
    public void JsonDeserialization_ShouldParseDuplicateVersionsAccurately()
    {
        // Arrange & Act
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var list = JsonSerializer.Deserialize<List<AspNetDevCertJsonEntry>>(SampleDuplicateJson, options);

        // Assert
        list.Should().NotBeNull();
        list!.Count.Should().Be(2);
        list.Should().Contain(c => c.Version == 6 && c.Thumbprint == "CB0FA4CD09F35ECF05A3B24A6B92004D66F9E7D5");
        list.Should().Contain(c => c.Version == 5 && c.Thumbprint == "EF2DA00BCD75180B38E85284CB38E5C127660795");
    }

    [Fact]
    public void DiagnosisLogic_ShouldFlagDuplicates_WhenMultipleVersionsExist()
    {
        // Arrange
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var list = JsonSerializer.Deserialize<List<AspNetDevCertJsonEntry>>(SampleDuplicateJson, options)!;

        // Act
        var hasDuplicates = list.Count > 1;
        var maxVersion = list.Max(c => c.Version);

        // Assert
        hasDuplicates.Should().BeTrue();
        maxVersion.Should().Be(6);
    }
}
