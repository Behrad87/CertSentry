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

    [RelayCommand]
    public void CopyDetails()
    {
        if (Result == null) return;
        var details = $"Endpoint: {Result.TargetUrl}\n" +
                      $"Protocol: {Result.TlsProtocolVersion}\n" +
                      $"Cipher Suite: {Result.NegotiatedCipherSuite}\n" +
                      $"Certificate: {Result.ServerCertificate?.Subject}\n" +
                      $"SANs: {Result.ServerCertificate?.SansDisplaySummary}\n" +
                      $"Verdict: {Result.VerdictTitle} - {Result.VerdictDescription}";
        Clipboard.SetText(details);
        CopyNotification = "Handshake report copied to clipboard!";
    }
}
