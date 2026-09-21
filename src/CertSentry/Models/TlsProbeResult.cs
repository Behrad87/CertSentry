using System.Collections.Generic;
using System.Net.Security;
using CertSentry.Enums;

namespace CertSentry.Models;

/// <summary>
/// Granular validation issue identified during TLS probe or certificate inspection.
/// </summary>
public class CertificateValidationIssue
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Remedy { get; set; } = string.Empty;
    public ProbeSeverity Severity { get; set; } = ProbeSeverity.Information;
}

/// <summary>
/// Result of an in-depth TLS handshake probe against a host:port or URL.
/// </summary>
public class TlsProbeResult
{
    public string TargetUrl { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
    public string ResolvedIp { get; set; } = string.Empty;

    public bool IsConnected { get; set; }
    public double DnsResolutionMs { get; set; }
    public double TcpConnectMs { get; set; }
    public double TlsHandshakeMs { get; set; }
    public double TotalDurationMs => DnsResolutionMs + TcpConnectMs + TlsHandshakeMs;

    public string TlsProtocolVersion { get; set; } = "None";
    public string NegotiatedCipherSuite { get; set; } = string.Empty;
    public string KeyExchangeAlgorithm { get; set; } = string.Empty;
    public string CipherAlgorithm { get; set; } = string.Empty;
    public int CipherStrength { get; set; }
    public string HashAlgorithm { get; set; } = string.Empty;
    public int HashStrength { get; set; }
    public string ApplicationProtocol { get; set; } = string.Empty;

    public SslPolicyErrors SslPolicyErrors { get; set; } = SslPolicyErrors.None;
    public CertificateItem? ServerCertificate { get; set; }
    public List<CertificateItem> CertificateChain { get; set; } = new();

    public bool IsHealthy => IsConnected && SslPolicyErrors == SslPolicyErrors.None && Issues.Count == 0;
    public string VerdictTitle { get; set; } = string.Empty;
    public string VerdictDescription { get; set; } = string.Empty;

    public List<CertificateValidationIssue> Issues { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
}
