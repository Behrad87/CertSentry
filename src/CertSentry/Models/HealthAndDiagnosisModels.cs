using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using CertSentry.Enums;

namespace CertSentry.Models;

/// <summary>
/// Raw item deserialized from `dotnet dev-certs https --check-trust-machine-readable`.
/// </summary>
public class AspNetDevCertJsonEntry
{
    [JsonPropertyName("Thumbprint")]
    public string Thumbprint { get; set; } = string.Empty;

    [JsonPropertyName("Subject")]
    public string Subject { get; set; } = string.Empty;

    [JsonPropertyName("X509SubjectAlternativeNameExtension")]
    public List<string> SubjectAlternativeNames { get; set; } = new();

    [JsonPropertyName("Version")]
    public int Version { get; set; }

    [JsonPropertyName("ValidityNotBefore")]
    public DateTime? ValidityNotBefore { get; set; }

    [JsonPropertyName("ValidityNotAfter")]
    public DateTime? ValidityNotAfter { get; set; }

    [JsonPropertyName("IsHttpsDevelopmentCertificate")]
    public bool IsHttpsDevelopmentCertificate { get; set; }

    [JsonPropertyName("IsExportable")]
    public bool IsExportable { get; set; }

    [JsonPropertyName("TrustLevel")]
    public string TrustLevel { get; set; } = string.Empty;
}

/// <summary>
/// Comprehensive evaluation of ASP.NET Core dev certificates on the system.
/// </summary>
public class AspNetDevCertDiagnosis
{
    public bool IsCliAvailable { get; set; }
    public bool HasCertificate { get; set; }
    public bool IsFullyTrusted { get; set; }
    public bool HasDuplicates { get; set; }
    public int ActiveVersion { get; set; }
    public List<AspNetDevCertJsonEntry> Certificates { get; set; } = new();
    public string SummaryMessage { get; set; } = string.Empty;
    public string RecommendedAction { get; set; } = string.Empty;
}

/// <summary>
/// Representation of a scanned network port.
/// </summary>
public class PortScanResult
{
    public int Port { get; set; }
    public string CommonServiceName { get; set; } = string.Empty;
    public PortServiceType Status { get; set; } = PortServiceType.Closed;
    public double LatencyMs { get; set; }
    public string CertificateSubject { get; set; } = string.Empty;
    public bool HasTlsError { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public string TargetUrl => $"https://localhost:{Port}";
}

/// <summary>
/// System-wide SSL/TLS health report generated for the Doctor Dashboard.
/// </summary>
public class SystemHealthReport
{
    public int HealthScore { get; set; } = 100;
    public string HealthLevel { get; set; } = "Healthy";
    public string HealthSummary { get; set; } = string.Empty;

    public int TotalDevCertsCount { get; set; }
    public int ValidDevCertsCount { get; set; }
    public int ExpiringSoonCount { get; set; }
    public int ExpiredCount { get; set; }
    public int DuplicateCount { get; set; }

    public bool AspNetCertInstalled { get; set; }
    public bool AspNetCertTrusted { get; set; }
    public bool AspNetHasDuplicates { get; set; }
    public string AspNetStatusText { get; set; } = string.Empty;

    public List<CertificateValidationIssue> ActiveIssues { get; set; } = new();
    public List<CertificateItem> KeyCertificates { get; set; } = new();
}
