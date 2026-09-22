using System;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CertSentry.Models;
using CertSentry.Services;
using Wpf.Ui.Abstractions.Controls;

namespace CertSentry.ViewModels;

public partial class TlsProbeViewModel : ObservableObject, INavigationAware
{
    private readonly ITlsProbeService _probeService;
    private readonly ICertificateStoreService _storeService;

    [ObservableProperty]
    private string _targetUrl = "https://localhost:5001";

    [ObservableProperty]
    private bool _isProbing;

    [ObservableProperty]
    private string _statusMessage = "Ready to probe localhost SSL/TLS endpoints.";

    [ObservableProperty]
    private TlsProbeResult? _result;

    [ObservableProperty]
    private string _copyNotification = string.Empty;

    public TlsProbeViewModel(ITlsProbeService probeService, ICertificateStoreService storeService)
    {
        _probeService = probeService;
        _storeService = storeService;
    }

    public void OnNavigatedTo() { }
    public Task OnNavigatedToAsync() => Task.CompletedTask;
    public void OnNavigatedFrom() { }
    public Task OnNavigatedFromAsync() => Task.CompletedTask;

    [RelayCommand]
    public async Task ProbeEndpointAsync()
    {
        if (string.IsNullOrWhiteSpace(TargetUrl)) return;

        IsProbing = true;
        CopyNotification = string.Empty;
        StatusMessage = $"Probing TLS handshake on {TargetUrl}...";

        try
        {
            Result = await _probeService.ProbeEndpointAsync(TargetUrl);
            StatusMessage = Result.IsConnected
                ? $"TLS Probe completed in {Result.TotalDurationMs} ms: {Result.VerdictTitle}"
                : $"Probe failed: {Result.VerdictTitle}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Probe encountered an error: {ex.Message}";
        }
        finally
        {
            IsProbing = false;
        }
    }

    [RelayCommand]
    public void SetPreset(string preset)
    {
        TargetUrl = preset;
        _ = ProbeEndpointAsync();
    }

    [RelayCommand]
    public async Task TrustServerCertificateAsync()
    {
        if (Result?.ServerCertificate == null) return;

        try
        {
            var success = await _storeService.TrustCertificateAsync(Result.ServerCertificate.Thumbprint);
            if (success)
            {
                CopyNotification = "Certificate installed into Windows CurrentUser\\Root store! Retesting...";
                await ProbeEndpointAsync();
            }
            else
            {
                CopyNotification = "Could not install certificate into root store.";
            }
        }
        catch (Exception ex)
        {
            CopyNotification = $"Trust error: {ex.Message}";
        }
    }

    [RelayCommand]
    public void CopyPem()
    {
        if (Result?.ServerCertificate?.RawPem is { } pem)
        {
            Clipboard.SetText(pem);
            CopyNotification = "Public certificate PEM copied to clipboard!";
        }
    }

    public string GenerateDiagnosticReport()
    {
        if (Result == null) return string.Empty;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=================================================");
        sb.AppendLine(" CertSentry TLS Endpoint Diagnostic Report");
        sb.AppendLine("=================================================");
        sb.AppendLine($"Target URL:           {Result.TargetUrl}");
        sb.AppendLine($"Resolved IP:          {Result.ResolvedIp}");
        sb.AppendLine($"Port:                 {Result.Port}");
        sb.AppendLine($"Connected:            {Result.IsConnected}");
        sb.AppendLine($"Verdict:              {Result.VerdictTitle}");
        sb.AppendLine($"Verdict Details:      {Result.VerdictDescription}");
        sb.AppendLine();
        sb.AppendLine("--- Latency Breakdown ---");
        sb.AppendLine($"DNS Resolution:       {Result.DnsResolutionMs} ms");
        sb.AppendLine($"TCP Connect:          {Result.TcpConnectMs} ms");
        sb.AppendLine($"TLS Handshake:        {Result.TlsHandshakeMs} ms");
        sb.AppendLine($"Total Probe Time:     {Result.TotalDurationMs} ms");
        sb.AppendLine();
        sb.AppendLine("--- Protocol & Cryptography ---");
        sb.AppendLine($"TLS Version:          {Result.TlsProtocolVersion}");
        sb.AppendLine($"ALPN Protocol:        {Result.ApplicationProtocol}");
        sb.AppendLine($"Cipher Suite:         {Result.NegotiatedCipherSuite}");
        sb.AppendLine($"Key Exchange:         {Result.KeyExchangeAlgorithm}");
        sb.AppendLine($"Cipher Algorithm:     {Result.CipherAlgorithm} ({Result.CipherStrength} bits)");
        sb.AppendLine($"Hash Algorithm:       {Result.HashAlgorithm} ({Result.HashStrength} bits)");
        sb.AppendLine($"SSL Policy Errors:    {Result.SslPolicyErrors}");
        sb.AppendLine();

        if (Result.ServerCertificate != null)
        {
            var cert = Result.ServerCertificate;
            sb.AppendLine("--- Server Certificate ---");
            sb.AppendLine($"Subject (CN):         {cert.CommonName}");
            sb.AppendLine($"Distinguished Name:   {cert.Subject}");
            sb.AppendLine($"Issuer:               {cert.Issuer}");
            sb.AppendLine($"Valid From:           {cert.NotBefore:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Valid To:             {cert.NotAfter:yyyy-MM-dd HH:mm:ss} ({cert.DaysUntilExpiration} days remaining)");
            sb.AppendLine($"Health Status:        {cert.HealthStatus}");
            sb.AppendLine($"SANs:                 {cert.SansDisplaySummary}");
            sb.AppendLine($"Thumbprint:           {cert.Thumbprint}");
            sb.AppendLine($"Key Type:             {cert.KeyAlgorithm} ({cert.KeySize}-bit)");
            sb.AppendLine($"Signature Algorithm:  {cert.SignatureAlgorithm}");
            sb.AppendLine($"Has Private Key:      {cert.HasPrivateKey}");
            sb.AppendLine();
        }

        if (Result.CertificateChain.Count > 0)
        {
            sb.AppendLine("--- Certificate Chain Hierarchy ---");
            for (int i = 0; i < Result.CertificateChain.Count; i++)
            {
                var c = Result.CertificateChain[i];
                sb.AppendLine($" [{i}] {c.CommonName} (Expires: {c.NotAfter:yyyy-MM-dd}) - {c.HealthStatus}");
            }
            sb.AppendLine();
        }

        if (Result.Issues.Count > 0)
        {
            sb.AppendLine("--- Diagnostic Issues & Remedies ---");
            foreach (var issue in Result.Issues)
            {
                sb.AppendLine($"[{issue.Severity}] {issue.Title}");
                sb.AppendLine($"  Description: {issue.Description}");
                sb.AppendLine($"  Remedy:      {issue.Remedy}");
            }
            sb.AppendLine();
        }

        if (Result.Recommendations.Count > 0)
        {
            sb.AppendLine("--- Recommendations ---");
            foreach (var rec in Result.Recommendations)
            {
                sb.AppendLine($"* {rec}");
            }
        }

        return sb.ToString();
    }

    [RelayCommand]
    public void CopyDetails()
    {
        if (Result == null) return;
        var report = GenerateDiagnosticReport();
        Clipboard.SetText(report);
        CopyNotification = "Full diagnostic report copied to clipboard!";
    }

    [RelayCommand]
    public void ExportReport()
    {
        if (Result == null) return;
        var sfd = new Microsoft.Win32.SaveFileDialog
        {
            FileName = $"TlsReport_{Result.Host}_{Result.Port}_{DateTime.Now:yyyyMMdd_HHmmss}.txt",
            Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
            Title = "Save TLS Diagnostic Report"
        };
        if (sfd.ShowDialog() == true)
        {
            System.IO.File.WriteAllText(sfd.FileName, GenerateDiagnosticReport());
            CopyNotification = $"Report saved to {System.IO.Path.GetFileName(sfd.FileName)}";
        }
    }
}
