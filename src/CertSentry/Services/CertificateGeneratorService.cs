using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using CertSentry.Enums;

namespace CertSentry.Services;

public class CertificateGenerationRequest
{
    public string CommonName { get; set; } = "localhost";
    public List<string> SubjectAlternativeNames { get; set; } = new() { "localhost", "127.0.0.1", "::1" };
    public KeyAlgorithmType KeyAlgorithm { get; set; } = KeyAlgorithmType.Rsa2048;
    public HashAlgorithmName HashAlgorithm { get; set; } = HashAlgorithmName.SHA256;
    public int ValidityDays { get; set; } = 365;
    public bool IsCertificateAuthority { get; set; }
    public string? FriendlyName { get; set; }
}

public interface ICertificateGeneratorService
{
    X509Certificate2 GenerateCertificate(CertificateGenerationRequest request);
    X509Certificate2 GenerateSignedLeafCertificate(CertificateGenerationRequest request, X509Certificate2 issuerCaCertificate);
    byte[] ExportToPfx(X509Certificate2 cert, string? password = null);
    (string CertificatePem, string PrivateKeyPem) ExportToPem(X509Certificate2 cert);
}

public class CertificateGeneratorService : ICertificateGeneratorService
{
    public X509Certificate2 GenerateCertificate(CertificateGenerationRequest request)
    {
        var subjectName = new X500DistinguishedName($"CN={request.CommonName}, O=CertSentry Localhost Dev, OU=Development");
        var notBefore = DateTimeOffset.UtcNow.AddMinutes(-5);
        var notAfter = notBefore.AddDays(request.ValidityDays);

        if (request.KeyAlgorithm is KeyAlgorithmType.Rsa2048 or KeyAlgorithmType.Rsa3072 or KeyAlgorithmType.Rsa4096)
        {
            int keySize = request.KeyAlgorithm switch
            {
                KeyAlgorithmType.Rsa3072 => 3072,
                KeyAlgorithmType.Rsa4096 => 4096,
                _ => 2048
            };

            using var rsa = RSA.Create(keySize);
            var certReq = new CertificateRequest(subjectName, rsa, request.HashAlgorithm, RSASignaturePadding.Pkcs1);
            ConfigureExtensions(certReq, request);

            if (request.IsCertificateAuthority)
            {
                var cert = certReq.CreateSelfSigned(notBefore, notAfter);
                return cert;
            }
            else
            {
                var cert = certReq.CreateSelfSigned(notBefore, notAfter);
                return cert;
            }
        }
        else
        {
            var curve = request.KeyAlgorithm switch
            {
                KeyAlgorithmType.EcdsaP384 => ECCurve.NamedCurves.nistP384,
                KeyAlgorithmType.EcdsaP521 => ECCurve.NamedCurves.nistP521,
                _ => ECCurve.NamedCurves.nistP256
            };

            using var ecdsa = ECDsa.Create(curve);
            var certReq = new CertificateRequest(subjectName, ecdsa, request.HashAlgorithm);
            ConfigureExtensions(certReq, request);

            var cert = certReq.CreateSelfSigned(notBefore, notAfter);
            return cert;
        }
    }

    public X509Certificate2 GenerateSignedLeafCertificate(CertificateGenerationRequest request, X509Certificate2 issuerCaCertificate)
    {
        if (!issuerCaCertificate.HasPrivateKey)
            throw new InvalidOperationException("Issuer CA certificate must possess a private key to sign leaf certificates.");

        var subjectName = new X500DistinguishedName($"CN={request.CommonName}, O=CertSentry Localhost Dev");
        var notBefore = DateTimeOffset.UtcNow.AddMinutes(-5);
        var notAfter = notBefore.AddDays(request.ValidityDays);

        // Ensure notAfter doesn't exceed CA's notAfter
        if (notAfter > issuerCaCertificate.NotAfter)
            notAfter = issuerCaCertificate.NotAfter;

        byte[] serialNumber = new byte[16];
        RandomNumberGenerator.Fill(serialNumber);

        if (request.KeyAlgorithm is KeyAlgorithmType.Rsa2048 or KeyAlgorithmType.Rsa3072 or KeyAlgorithmType.Rsa4096)
        {
            int keySize = request.KeyAlgorithm switch
            {
                KeyAlgorithmType.Rsa3072 => 3072,
                KeyAlgorithmType.Rsa4096 => 4096,
                _ => 2048
            };

            using var rsa = RSA.Create(keySize);
            var certReq = new CertificateRequest(subjectName, rsa, request.HashAlgorithm, RSASignaturePadding.Pkcs1);
            ConfigureExtensions(certReq, request);

            using var signedCert = certReq.Create(issuerCaCertificate, notBefore, notAfter, serialNumber);
            return signedCert.CopyWithPrivateKey(rsa);
        }
        else
        {
            var curve = request.KeyAlgorithm switch
            {
                KeyAlgorithmType.EcdsaP384 => ECCurve.NamedCurves.nistP384,
                KeyAlgorithmType.EcdsaP521 => ECCurve.NamedCurves.nistP521,
                _ => ECCurve.NamedCurves.nistP256
            };

            using var ecdsa = ECDsa.Create(curve);
            var certReq = new CertificateRequest(subjectName, ecdsa, request.HashAlgorithm);
            ConfigureExtensions(certReq, request);

            using var signedCert = certReq.Create(issuerCaCertificate, notBefore, notAfter, serialNumber);
            return signedCert.CopyWithPrivateKey(ecdsa);
        }
    }

    private void ConfigureExtensions(CertificateRequest certReq, CertificateGenerationRequest request)
    {
        // Basic Constraints
        certReq.CertificateExtensions.Add(new X509BasicConstraintsExtension(
            certificateAuthority: request.IsCertificateAuthority,
            hasPathLengthConstraint: false,
            pathLengthConstraint: 0,
            critical: true));

        // Key Usage
        if (request.IsCertificateAuthority)
        {
            certReq.CertificateExtensions.Add(new X509KeyUsageExtension(
                X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign | X509KeyUsageFlags.DigitalSignature,
                critical: true));
        }
        else
        {
            var keyUsage = X509KeyUsageFlags.DigitalSignature;
            if (request.KeyAlgorithm is KeyAlgorithmType.Rsa2048 or KeyAlgorithmType.Rsa3072 or KeyAlgorithmType.Rsa4096)
                keyUsage |= X509KeyUsageFlags.KeyEncipherment;

            certReq.CertificateExtensions.Add(new X509KeyUsageExtension(keyUsage, critical: true));

            // Enhanced Key Usage: Server Authentication and Client Authentication
            var eku = new OidCollection
            {
                new Oid("1.3.6.1.5.5.7.3.1", "Server Authentication"),
                new Oid("1.3.6.1.5.5.7.3.2", "Client Authentication")
            };
            certReq.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(eku, critical: false));

            // Subject Alternative Names (SAN)
            var sanBuilder = new SubjectAlternativeNameBuilder();
            bool hasSans = false;
            foreach (var san in request.SubjectAlternativeNames)
            {
                var trimmed = san.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                if (IPAddress.TryParse(trimmed, out var ip))
                {
                    sanBuilder.AddIpAddress(ip);
                    hasSans = true;
                }
                else
                {
                    sanBuilder.AddDnsName(trimmed);
                    hasSans = true;
                }
            }

            if (hasSans)
            {
                certReq.CertificateExtensions.Add(sanBuilder.Build());
            }
        }

        // Subject Key Identifier
        certReq.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(certReq.PublicKey, false));
    }

    public byte[] ExportToPfx(X509Certificate2 cert, string? password = null)
    {
        return cert.Export(X509ContentType.Pfx, password ?? string.Empty);
    }

    public (string CertificatePem, string PrivateKeyPem) ExportToPem(X509Certificate2 cert)
    {
        var certPem = cert.ExportCertificatePem();
        string keyPem = string.Empty;

        if (cert.GetRSAPrivateKey() is { } rsa)
        {
            keyPem = rsa.ExportPkcs8PrivateKeyPem();
        }
        else if (cert.GetECDsaPrivateKey() is { } ecdsa)
        {
            keyPem = ecdsa.ExportPkcs8PrivateKeyPem();
        }

        return (certPem, keyPem);
    }
}
