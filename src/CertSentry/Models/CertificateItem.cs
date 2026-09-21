using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using CertSentry.Enums;

namespace CertSentry.Models;

/// <summary>
/// Detailed representation of a Windows Certificate Store item.
/// </summary>
public class CertificateItem
{
    public string Thumbprint { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string CommonName { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string FriendlyName { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;

    public DateTime NotBefore { get; set; }
    public DateTime NotAfter { get; set; }
    public int DaysUntilExpiration => (int)Math.Ceiling((NotAfter - DateTime.Now).TotalDays);

    public List<string> SubjectAlternativeNames { get; set; } = new();
    public List<string> EnhancedKeyUsages { get; set; } = new();

    public string KeyAlgorithm { get; set; } = string.Empty;
    public int KeySize { get; set; }
    public string SignatureAlgorithm { get; set; } = string.Empty;

    public bool HasPrivateKey { get; set; }
    public bool IsSelfSigned { get; set; }
    public bool IsAspNetCoreDevCert { get; set; }
    public bool IsLocalhostCert { get; set; }
    public bool IsCertificateAuthority { get; set; }

    public string StoreName { get; set; } = string.Empty;
    public StoreLocation StoreLocation { get; set; }

    public CertificateHealthStatus HealthStatus { get; set; } = CertificateHealthStatus.Unknown;
    public string StatusMessage { get; set; } = string.Empty;

    public string RawPem { get; set; } = string.Empty;

    public string ExpirationSummary
    {
        get
        {
            if (DaysUntilExpiration < 0)
                return $"Expired {Math.Abs(DaysUntilExpiration)} days ago ({NotAfter:yyyy-MM-dd})";
            if (DaysUntilExpiration == 0)
                return $"Expires today ({NotAfter:yyyy-MM-dd HH:mm})";
            if (DaysUntilExpiration == 1)
                return $"Expires tomorrow ({NotAfter:yyyy-MM-dd})";
            return $"Expires in {DaysUntilExpiration} days ({NotAfter:yyyy-MM-dd})";
        }
    }

    public string SansDisplaySummary => SubjectAlternativeNames.Count > 0
        ? string.Join(", ", SubjectAlternativeNames)
        : "(No SANs)";
}
