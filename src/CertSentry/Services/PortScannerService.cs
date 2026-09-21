using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using CertSentry.Enums;
using CertSentry.Models;

namespace CertSentry.Services;

public interface IPortScannerService
{
    IReadOnlyList<int> DefaultDevPorts { get; }
    Task<List<PortScanResult>> ScanPortsAsync(IEnumerable<int> ports, IProgress<PortScanResult>? progress = null, CancellationToken cancellationToken = default);
    Task<PortScanResult> ScanSinglePortAsync(int port, int timeoutMs = 1200, CancellationToken cancellationToken = default);
}

public class PortScannerService : IPortScannerService
{
    public IReadOnlyList<int> DefaultDevPorts { get; } = new List<int>
    {
        3000, 3001, 4200, 443, 5000, 5001, 5173, 7000, 7001, 7100, 8000, 8080, 8443, 9000, 9443
    };

    private static readonly Dictionary<int, string> KnownPortDescriptions = new()
    {
        { 443, "HTTPS Default" },
        { 3000, "React / Next.js / Node.js" },
        { 3001, "Node.js Alt / React" },
        { 4200, "Angular CLI" },
        { 5000, "ASP.NET Core HTTP / Flask" },
        { 5001, "ASP.NET Core HTTPS" },
        { 5173, "Vite Dev Server" },
        { 7000, "ASP.NET Core Minimal API" },
        { 7001, "ASP.NET Core HTTPS Alt" },
        { 7100, "Blazor Dev Server" },
        { 8000, "Django / Python Dev Server" },
        { 8080, "Tomcat / Spring Boot / Vue" },
        { 8443, "Dev Proxy / Caddy / Tomcat SSL" },
        { 9000, "Portainer / PHP-FPM" },
        { 9443, "Portainer HTTPS / Traefik" }
    };

    public async Task<List<PortScanResult>> ScanPortsAsync(IEnumerable<int> ports, IProgress<PortScanResult>? progress = null, CancellationToken cancellationToken = default)
    {
        var portList = new List<int>(ports);
        var results = new List<PortScanResult>();
        using var semaphore = new SemaphoreSlim(6);

        var tasks = portList.Select(async port =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                var res = await ScanSinglePortAsync(port, timeoutMs: 1200, cancellationToken);
                lock (results)
                {
                    results.Add(res);
                }
                progress?.Report(res);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
        return results.OrderBy(r => r.Port).ToList();
    }

    public async Task<PortScanResult> ScanSinglePortAsync(int port, int timeoutMs = 1200, CancellationToken cancellationToken = default)
    {
        var result = new PortScanResult
        {
            Port = port,
            CommonServiceName = KnownPortDescriptions.TryGetValue(port, out var desc) ? desc : "Custom Dev Service"
        };

        var sw = Stopwatch.StartNew();

        try
        {
            using var tcpClient = new TcpClient();
            using var connectCts = new CancellationTokenSource(timeoutMs);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, connectCts.Token);

            await tcpClient.ConnectAsync("127.0.0.1", port, linkedCts.Token);
            sw.Stop();
            result.LatencyMs = Math.Round(sw.Elapsed.TotalMilliseconds, 1);

            // TCP Connected! Now test if TLS is supported on this port
            string? certSubject = null;
            bool isTls = false;
            bool hasTlsError = false;

            try
            {
                using var netStream = tcpClient.GetStream();
                using var sslStream = new SslStream(netStream, false, (s, cert, chain, errs) =>
                {
                    if (cert != null)
                    {
                        certSubject = cert.Subject;
                    }
                    hasTlsError = errs != SslPolicyErrors.None;
                    return true;
                });

                using var handshakeCts = new CancellationTokenSource(1000);
                using var linkedHandshakeCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, handshakeCts.Token);

                await sslStream.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
                {
                    TargetHost = "localhost",
                    EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
                }, linkedHandshakeCts.Token);

                isTls = true;
            }
            catch (Exception ex)
            {
                // If handshake failed, could be plaintext HTTP
                if (certSubject != null)
                {
                    isTls = true;
                    hasTlsError = true;
                }
                else
                {
                    result.ErrorMessage = ex.Message;
                }
            }

            if (isTls)
            {
                result.Status = PortServiceType.Https;
                result.CertificateSubject = certSubject ?? "Unknown Subject";
                result.HasTlsError = hasTlsError;
            }
            else
            {
                result.Status = PortServiceType.Http;
            }
        }
        catch (Exception)
        {
            sw.Stop();
            result.LatencyMs = Math.Round(sw.Elapsed.TotalMilliseconds, 1);
            result.Status = PortServiceType.Closed;
        }

        return result;
    }
}
