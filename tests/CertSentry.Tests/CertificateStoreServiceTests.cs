using System.Security.Cryptography.X509Certificates;
using CertSentry.Enums;
using CertSentry.Services;
using FluentAssertions;
using Xunit;

namespace CertSentry.Tests;

public class CertificateStoreServiceTests
{
    private readonly CertificateStoreService _storeService = new();
    private readonly CertificateGeneratorService _genService = new();

    [Fact]
    public void IsDevOrLocalhostCertificate_WithLocalhostSubject_ShouldReturnTrue()
    {
        // Arrange
        var req = new CertificateGenerationRequest
        {
            CommonName = "localhost",
            SubjectAlternativeNames = new() { "localhost", "127.0.0.1" }
        };
        using var cert = _genService.GenerateCertificate(req);

        // Act
        var isDev = _storeService.IsDevOrLocalhostCertificate(cert);

        // Assert
        isDev.Should().BeTrue();
    }

    [Fact]
    public void IsDevOrLocalhostCertificate_WithProductionExternalSubject_ShouldReturnFalse()
    {
        // Arrange: Create a non-dev external certificate
        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        var subject = new X500DistinguishedName("CN=production.example.com, O=Example Corporation");
        var req = new CertificateRequest(subject, rsa, System.Security.Cryptography.HashAlgorithmName.SHA256, System.Security.Cryptography.RSASignaturePadding.Pkcs1);
        using var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));

        // Act
        var isDev = _storeService.IsDevOrLocalhostCertificate(cert);

        // Assert
        isDev.Should().BeFalse();
    }

    [Fact]
    public void ConvertToModel_ShouldExtractKeyProperties()
    {
        // Arrange
        var req = new CertificateGenerationRequest
        {
            CommonName = "dev.test",
            SubjectAlternativeNames = new() { "dev.test", "127.0.0.1" },
            KeyAlgorithm = KeyAlgorithmType.Rsa2048,
            ValidityDays = 120
        };
        using var cert = _genService.GenerateCertificate(req);

        // Act
        var model = _storeService.ConvertToModel(cert, "My", StoreLocation.CurrentUser);

        // Assert
        model.Should().NotBeNull();
        model.CommonName.Should().Be("dev.test");
        model.KeyAlgorithm.Should().Be("RSA");
        model.KeySize.Should().Be(2048);
        model.SubjectAlternativeNames.Should().Contain("dev.test");
        model.SubjectAlternativeNames.Should().Contain("127.0.0.1");
        model.HealthStatus.Should().Be(CertificateHealthStatus.Valid);
        model.HasPrivateKey.Should().BeTrue();
    }
}
