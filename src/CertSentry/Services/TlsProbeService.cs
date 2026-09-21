using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CertSentry.Enums;
using CertSentry.Models;

namespace CertSentry.Services;

public interface ITlsProbeService
{
    Task<TlsProbeResult> ProbeEndpointAsync(string target, int timeoutMs = 6000, CancellationToken cancellationToken = default);
    bool IsHostMatchingSan(string host, IEnumerable<string> sans);
}

public class TlsProbeService : ITlsProbeService
{
    private readonly ICertificateStoreService _storeService;

    public TlsProbeService(ICertificateStoreService storeService)
    {
        _storeService = storeService;
    }

    public async Task<TlsProbeResult> ProbeEndpointAsync(string target, int timeoutMs = 6000, CancellationToken cancellationToken = default)
    {
        var result = new TlsProbeResult();

        // 1. Parse target URL or host:port
        string host = target.Trim();
        int port = 443;

        if (host.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            if (Uri.TryCreate(host, UriKind.Absolute, out var uri))
            {
                host = uri.DnsSafeHost;
                port = uri.Port > 0 ? uri.Port : 443;
            }
        }
        else if (host.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            if (Uri.TryCreate(host, UriKind.Absolute, out var uri))
            {
                host = uri.DnsSafeHost;
                port = uri.Port > 0 ? uri.Port : 80;
            }
        }
        else if (host.Contains(':'))
        {
            var parts = host.Split(':');
            host = parts[0];
            if (parts.Length > 1 && int.TryParse(parts[1], out var p))
                port = p;
        }

        result.Host = host;
        result.Port = port;
        result.TargetUrl = $"https://{host}:{port}";

        var swDns = Stopwatch.StartNew();
        IPAddress? ip = null;
        try
        {
            if (IPAddress.TryParse(host, out var parsedIp))
            {
                ip = parsedIp;
            }
            else
            {
                var entry = await Dns.GetHostEntryAsync(host, cancellationToken);
                ip = entry.AddressList.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)
                     ?? entry.AddressList.FirstOrDefault();
            }
        }
        catch (Exception ex)
        {
            result.Issues.Add(new CertificateValidationIssue
            {
                Title = "DNS Resolution Failed",
                Description = $"Could not resolve hostname '{host}': {ex.Message}",
                Remedy = "Check your hosts file (C:\\Windows\\System32\\drivers\\etc\\hosts) or local DNS settings.",
                Severity = ProbeSeverity.Error
            });
            result.VerdictTitle = "DNS Error";
            result.VerdictDescription = $"Hostname '{host}' cannot be resolved to an IP address.";
            return result;
        }
        swDns.Stop();
        result.DnsResolutionMs = Math.Round(swDns.Elapsed.TotalMilliseconds, 2);
        result.ResolvedIp = ip?.ToString() ?? "Unknown";

        // 2. TCP Connection
        using var tcpClient = new TcpClient();
        var swTcp = Stopwatch.StartNew();
        try
        {
            using var timeoutCts = new CancellationTokenSource(timeoutMs);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
            await tcpClient.ConnectAsync(ip!, port, linkedCts.Token);
        }
        catch (Exception ex)
        {
            swTcp.Stop();
            result.TcpConnectMs = Math.Round(swTcp.Elapsed.TotalMilliseconds, 2);
            result.Issues.Add(new CertificateValidationIssue
            {
                Title = "Connection Refused / Timeout",
                Description = $"Failed to establish TCP socket with {ip}:{port}: {ex.Message}",
                Remedy = "Ensure your local web server is running and listening on the specified port.",
                Severity = ProbeSeverity.Error
            });
            result.VerdictTitle = "Connection Failed";
            result.VerdictDescription = $"TCP connection to {host}:{port} was refused or timed out.";
            return result;
        }
        swTcp.Stop();
        result.TcpConnectMs = Math.Round(swTcp.Elapsed.TotalMilliseconds, 2);

        // 3. TLS Handshake
        var swTls = Stopwatch.StartNew();
        X509Certificate2? remoteCertCaptured = null;
        X509Chain? chainCaptured = null;
        SslPolicyErrors sslErrorsCaptured = SslPolicyErrors.None;

        try
        {
            using var netStream = tcpClient.GetStream();
            using var sslStream = new SslStream(netStream, false, (sender, certificate, chain, sslPolicyErrors) =>
            {
                if (certificate != null)
                {
                    remoteCertCaptured = new X509Certificate2(certificate);
                }
                if (chain != null)
                {
                    chainCaptured = new X509Chain();
                    chainCaptured.Build(remoteCertCaptured ?? new X509Certificate2(certificate!));
                }
                sslErrorsCaptured = sslPolicyErrors;
                return true; // continue handshake so we can inspect everything
            });

            var sslOptions = new SslClientAuthenticationOptions
            {
                TargetHost = host,
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                ApplicationProtocols = new List<SslApplicationProtocol>
                {
                    SslApplicationProtocol.Http2,
                    SslApplicationProtocol.Http11
                }
            };

            using var handshakeCts = new CancellationTokenSource(timeoutMs);
            using var linkedTlsCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, handshakeCts.Token);
            await sslStream.AuthenticateAsClientAsync(sslOptions, linkedTlsCts.Token);

            swTls.Stop();
            result.TlsHandshakeMs = Math.Round(swTls.Elapsed.TotalMilliseconds, 2);
            result.IsConnected = true;

            // TLS details
            result.TlsProtocolVersion = sslStream.SslProtocol.ToString();
            result.NegotiatedCipherSuite = sslStream.NegotiatedCipherSuite.ToString();
#pragma warning disable SYSLIB0058
            result.KeyExchangeAlgorithm = sslStream.KeyExchangeAlgorithm.ToString();
            result.CipherAlgorithm = sslStream.CipherAlgorithm.ToString();
            result.CipherStrength = sslStream.CipherStrength;
            result.HashAlgorithm = sslStream.HashAlgorithm.ToString();
            result.HashStrength = sslStream.HashStrength;
#pragma warning restore SYSLIB0058

            if (sslStream.NegotiatedApplicationProtocol == SslApplicationProtocol.Http2)
                result.ApplicationProtocol = "HTTP/2 (h2)";
            else if (sslStream.NegotiatedApplicationProtocol == SslApplicationProtocol.Http11)
                result.ApplicationProtocol = "HTTP/1.1";
            else
                result.ApplicationProtocol = sslStream.NegotiatedApplicationProtocol.ToString();

            result.SslPolicyErrors = sslErrorsCaptured;

            // Convert server certificate
            if (remoteCertCaptured != null)
            {
                result.ServerCertificate = _storeService.ConvertToModel(remoteCertCaptured, "Remote", StoreLocation.CurrentUser);
            }

            // Chain hierarchy
            if (chainCaptured != null)
            {
                foreach (var element in chainCaptured.ChainElements)
                {
                    result.CertificateChain.Add(_storeService.ConvertToModel(element.Certificate, "Chain", StoreLocation.CurrentUser));
                }
            }

            // Run Diagnostics
            AnalyzeTlsDiagnostics(result, host);
        }
        catch (Exception ex)
        {
            swTls.Stop();
            result.TlsHandshakeMs = Math.Round(swTls.Elapsed.TotalMilliseconds, 2);
            result.Issues.Add(new CertificateValidationIssue
            {
                Title = "TLS Handshake Failed",
                Description = $"SSL/TLS handshake with {host}:{port} failed: {ex.Message}",
                Remedy = "Verify the target port is configured for HTTPS/TLS, or check if the server requires client certificate.",
                Severity = ProbeSeverity.Error
            });
            result.VerdictTitle = "Handshake Failed";
            result.VerdictDescription = ex.Message;
        }

        return result;
    }

    private void AnalyzeTlsDiagnostics(TlsProbeResult result, string host)
    {
        if (result.ServerCertificate == null)
        {
            result.Issues.Add(new CertificateValidationIssue
            {
                Title = "No Server Certificate Provided",
                Description = "The server completed a handshake but did not present a public X.509 certificate.",
                Remedy = "Configure your web server with an SSL certificate.",
                Severity = ProbeSeverity.Error
            });
            result.VerdictTitle = "No Certificate";
            return;
        }

        var cert = result.ServerCertificate;

        // 1. Expiration Checks
        if (cert.DaysUntilExpiration < 0)
        {
            result.Issues.Add(new CertificateValidationIssue
            {
                Title = "Certificate Expired",
                Description = $"The certificate expired {Math.Abs(cert.DaysUntilExpiration)} days ago on {cert.NotAfter:yyyy-MM-dd HH:mm}.",
                Remedy = "Renew or regenerate the certificate. For ASP.NET Core, run 'dotnet dev-certs https --trust' or use CertSentry 1-Click Fix.",
                Severity = ProbeSeverity.Error
            });
            result.Recommendations.Add("Regenerate the expired certificate to prevent browser SEC_ERROR_EXPIRED_CERTIFICATE warnings.");
        }
        else if (cert.DaysUntilExpiration <= 14)
        {
            result.Issues.Add(new CertificateValidationIssue
            {
                Title = "Certificate Expiring Soon",
                Description = $"The certificate will expire in {cert.DaysUntilExpiration} days ({cert.NotAfter:yyyy-MM-dd}).",
                Remedy = "Plan renewal soon to prevent localhost development downtime.",
                Severity = ProbeSeverity.Warning
            });
        }

        // 2. SAN Matching
        bool sanMatches = IsHostMatchingSan(host, cert.SubjectAlternativeNames);
        if (!sanMatches)
        {
            result.Issues.Add(new CertificateValidationIssue
            {
                Title = "Subject Alternative Name (SAN) Mismatch",
                Description = $"The requested hostname '{host}' is NOT covered by the certificate's SANs: [{cert.SansDisplaySummary}].",
                Remedy = $"Generate a certificate that includes '{host}' in its Subject Alternative Names list.",
                Severity = ProbeSeverity.Error
            });
            result.Recommendations.Add($"Use CertSentry's Certificate Generator to issue a cert with SAN '{host}'.");
        }

        // 3. SSL Policy Errors Check
        if (result.SslPolicyErrors.HasFlag(SslPolicyErrors.RemoteCertificateChainErrors))
        {
            result.Issues.Add(new CertificateValidationIssue
            {
                Title = "Untrusted Certificate Authority / Root Chain Error",
                Description = "The certificate authority chain could not be verified up to a trusted Windows Root Certification Authority.",
                Remedy = "Install the issuing Root CA or self-signed certificate into the Windows 'Trusted Root Certification Authorities' store.",
                Severity = ProbeSeverity.Error
            });
            result.Recommendations.Add("Use CertSentry's Store Explorer to 1-click trust this certificate into CurrentUser\\Root.");
        }

        if (result.SslPolicyErrors.HasFlag(SslPolicyErrors.RemoteCertificateNameMismatch) && sanMatches)
        {
            // Name mismatch flagged by OS policy
            result.Issues.Add(new CertificateValidationIssue
            {
                Title = "Remote Certificate Name Mismatch",
                Description = $"Operating system TLS validator rejected name '{host}' against certificate subject.",
                Remedy = $"Ensure certificate SAN explicitly lists '{host}'.",
                Severity = ProbeSeverity.Warning
            });
        }

        // 4. Protocols & Ciphers
        if (result.TlsProtocolVersion.Contains("Tls13"))
        {
            result.Recommendations.Add("Modern TLS 1.3 negotiated: Optimal security and low latency (0-RTT/1-RTT).");
        }

        // Final verdict calculation
        if (result.Issues.Any(i => i.Severity == ProbeSeverity.Error))
        {
            result.VerdictTitle = "Connection Issues Detected";
            result.VerdictDescription = "Browsers and API clients (Node, cURL, Docker) will likely reject this localhost connection.";
        }
        else if (result.Issues.Any(i => i.Severity == ProbeSeverity.Warning))
        {
            result.VerdictTitle = "Connected with Warnings";
            result.VerdictDescription = "Connection is functional but has warnings that require attention soon.";
        }
        else
        {
            result.VerdictTitle = "Secure & Trusted (Healthy)";
            result.VerdictDescription = "Localhost TLS handshake succeeded with full chain trust and valid SAN coverage.";
        }
    }

    public bool IsHostMatchingSan(string host, IEnumerable<string> sans)
    {
        var target = host.Trim().ToLowerInvariant();
        var sanList = sans.Select(s => s.Trim().ToLowerInvariant()).ToList();

        if (sanList.Count == 0)
            return false;

        // Exact match
        if (sanList.Contains(target))
            return true;

        // Loopback alias matching: localhost <=> 127.0.0.1 <=> ::1
        if (target == "localhost" && (sanList.Contains("127.0.0.1") || sanList.Contains("::1")))
            return true;
        if (target == "127.0.0.1" && sanList.Contains("localhost"))
            return true;

        // Wildcard matching: *.dev.localhost matches test.dev.localhost
        foreach (var san in sanList)
        {
            if (san.StartsWith("*."))
            {
                var suffix = san.Substring(2);
                var regex = new Regex("^[^.]+\\." + Regex.Escape(suffix) + "$", RegexOptions.IgnoreCase);
                if (regex.IsMatch(target))
                    return true;
            }
        }

        return false;
    }
}
