using System;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using CertSentry.Enums;
using CertSentry.Services;
using FluentAssertions;
using Xunit;

namespace CertSentry.Tests;

public class CertificateGeneratorServiceTests
{
    private readonly CertificateGeneratorService _service = new();

    [Fact]
    public void GenerateCertificate_Rsa2048_ShouldCreateValidCertificateWithSans()
    {
        // Arrange
        var request = new CertificateGenerationRequest
        {
            CommonName = "localhost",
            SubjectAlternativeNames = new() { "localhost", "127.0.0.1", "dev.local", "::1" },
            KeyAlgorithm = KeyAlgorithmType.Rsa2048,
            ValidityDays = 365,
            IsCertificateAuthority = false
        };

        // Act
        using var cert = _service.GenerateCertificate(request);

        // Assert
        cert.Should().NotBeNull();
        cert.Subject.Should().Contain("CN=localhost");
        cert.HasPrivateKey.Should().BeTrue();
        cert.NotAfter.Should().BeAfter(DateTime.UtcNow.AddDays(360));

        var sanExt = cert.Extensions.OfType<X509SubjectAlternativeNameExtension>().FirstOrDefault();
        sanExt.Should().NotBeNull();

        var dnsNames = sanExt!.EnumerateDnsNames().ToList();
        dnsNames.Should().Contain("localhost");
        dnsNames.Should().Contain("dev.local");

        var ips = sanExt.EnumerateIPAddresses().Select(ip => ip.ToString()).ToList();
        ips.Should().Contain("127.0.0.1");
        ips.Should().Contain("::1");
    }

    [Fact]
    public void GenerateCertificate_EcdsaP256_ShouldCreateValidCertificate()
    {
        // Arrange
        var request = new CertificateGenerationRequest
        {
            CommonName = "test.local",
            SubjectAlternativeNames = new() { "test.local" },
            KeyAlgorithm = KeyAlgorithmType.EcdsaP256,
            ValidityDays = 90
        };

        // Act
        using var cert = _service.GenerateCertificate(request);

        // Assert
        cert.Should().NotBeNull();
        cert.Subject.Should().Contain("CN=test.local");
        cert.GetECDsaPublicKey().Should().NotBeNull();
    }

    [Fact]
    public void GenerateCertificate_AsCertificateAuthority_ShouldHaveCaBasicConstraints()
    {
        // Arrange
        var request = new CertificateGenerationRequest
        {
            CommonName = "CertSentry Test Root CA",
            IsCertificateAuthority = true,
            ValidityDays = 1825
        };

        // Act
        using var cert = _service.GenerateCertificate(request);

        // Assert
        cert.Should().NotBeNull();
        var bcExt = cert.Extensions.OfType<X509BasicConstraintsExtension>().FirstOrDefault();
        bcExt.Should().NotBeNull();
        bcExt!.CertificateAuthority.Should().BeTrue();

        var kuExt = cert.Extensions.OfType<X509KeyUsageExtension>().FirstOrDefault();
        kuExt.Should().NotBeNull();
        kuExt!.KeyUsages.HasFlag(X509KeyUsageFlags.KeyCertSign).Should().BeTrue();
    }

    [Fact]
    public void GenerateSignedLeafCertificate_SignedByCA_ShouldHaveIssuerMatchingCa()
    {
        // Arrange: generate CA
        var caReq = new CertificateGenerationRequest
        {
            CommonName = "CertSentry Root CA",
            IsCertificateAuthority = true,
            ValidityDays = 1000
        };
        using var caCert = _service.GenerateCertificate(caReq);

        var leafReq = new CertificateGenerationRequest
        {
            CommonName = "api.local",
            SubjectAlternativeNames = new() { "api.local" },
            ValidityDays = 365,
            IsCertificateAuthority = false
        };

        // Act
        using var leafCert = _service.GenerateSignedLeafCertificate(leafReq, caCert);

        // Assert
        leafCert.Should().NotBeNull();
        leafCert.Subject.Should().Contain("CN=api.local");
        leafCert.Issuer.Should().Contain("CN=CertSentry Root CA");
        leafCert.HasPrivateKey.Should().BeTrue();
    }

    [Fact]
    public void ExportToPfxAndPem_ShouldProduceValidOutputs()
    {
        // Arrange
        var request = new CertificateGenerationRequest
        {
            CommonName = "export-test.local",
            SubjectAlternativeNames = new() { "export-test.local" }
        };
        using var cert = _service.GenerateCertificate(request);

        // Act: PFX
        var pfxBytes = _service.ExportToPfx(cert, "P@ssw0rd123");
        pfxBytes.Should().NotBeEmpty();

        // Verify PFX can be loaded back
        using var loadedPfx = X509CertificateLoader.LoadPkcs12(pfxBytes, "P@ssw0rd123");
        loadedPfx.Subject.Should().Contain("CN=export-test.local");

        // Act: PEM
        var (certPem, keyPem) = _service.ExportToPem(cert);
        certPem.Should().Contain("-----BEGIN CERTIFICATE-----");
        certPem.Should().Contain("-----END CERTIFICATE-----");
        keyPem.Should().Contain("-----BEGIN PRIVATE KEY-----");
    }
}
