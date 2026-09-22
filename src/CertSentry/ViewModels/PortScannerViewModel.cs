using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CertSentry.Enums;
using CertSentry.Models;
using CertSentry.Services;
using Wpf.Ui.Abstractions.Controls;

namespace CertSentry.ViewModels;

public partial class PortScannerViewModel : ObservableObject, INavigationAware
{
    private readonly IPortScannerService _scannerService;
    private readonly Wpf.Ui.INavigationService? _navigationService;
    private readonly TlsProbeViewModel? _tlsProbeViewModel;
    private CancellationTokenSource? _scanCts;

    [ObservableProperty]
    private ObservableCollection<PortScanResult> _results = new();

    [ObservableProperty]
    private PortScanResult? _selectedResult;

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private string _statusMessage = "Ready to scan localhost development ports.";

    [ObservableProperty]
    private string _customPortInput = "3000, 5001, 5173, 8080";

    [ObservableProperty]
    private string _notificationMessage = string.Empty;

    public PortScannerViewModel(
        IPortScannerService scannerService,
        Wpf.Ui.INavigationService? navigationService = null,
        TlsProbeViewModel? tlsProbeViewModel = null)
    {
        _scannerService = scannerService;
        _navigationService = navigationService;
        _tlsProbeViewModel = tlsProbeViewModel;
    }

    public void OnNavigatedTo()
    {
        if (Results.Count == 0)
        {
            _ = ScanDefaultPortsAsync();
        }
    }

    public Task OnNavigatedToAsync() => Task.CompletedTask;
    public void OnNavigatedFrom()
    {
        _scanCts?.Cancel();
    }
    public Task OnNavigatedFromAsync() => Task.CompletedTask;

    [RelayCommand]
    public async Task ScanDefaultPortsAsync()
    {
        await ExecuteScanAsync(_scannerService.DefaultDevPorts);
    }

    [RelayCommand]
    public async Task ScanCustomPortsAsync()
    {
        var ports = ParsePorts(CustomPortInput);
        if (ports.Count == 0)
        {
            NotificationMessage = "Please enter valid port numbers (e.g. 3000, 5000-5010, 8080).";
            return;
        }

        await ExecuteScanAsync(ports);
    }

    [RelayCommand]
    public void CancelScan()
    {
        _scanCts?.Cancel();
        IsScanning = false;
        StatusMessage = "Scan cancelled by user.";
    }

    [RelayCommand]
    public void CopyPortUrl(PortScanResult? item)
    {
        if (item != null)
        {
            Clipboard.SetText(item.TargetUrl);
            NotificationMessage = $"Copied {item.TargetUrl} to clipboard!";
        }
    }

    [RelayCommand]
    public async Task ProbePortInDoctorAsync(PortScanResult? item)
    {
        if (item == null) return;

        if (_tlsProbeViewModel != null)
        {
            _tlsProbeViewModel.TargetUrl = item.TargetUrl;
            _ = _tlsProbeViewModel.ProbeEndpointAsync();
        }

        _navigationService?.Navigate(typeof(Views.Pages.TlsProbePage));
    }

    private async Task ExecuteScanAsync(IEnumerable<int> ports)
    {
        _scanCts?.Cancel();
        _scanCts = new CancellationTokenSource();

        IsScanning = true;
        NotificationMessage = string.Empty;
        Results.Clear();
        StatusMessage = "Scanning localhost ports for active HTTPS/HTTP services...";

        var progress = new Progress<PortScanResult>(item =>
        {
            Results.Add(item);
        });

        try
        {
            var scanned = await _scannerService.ScanPortsAsync(ports, progress, _scanCts.Token);
            var httpsCount = scanned.Count(r => r.Status == PortServiceType.Https);
            var httpCount = scanned.Count(r => r.Status == PortServiceType.Http);
            StatusMessage = $"Scan completed: {httpsCount} HTTPS service(s), {httpCount} HTTP service(s) detected.";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Scan cancelled.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Scan error: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }

    private static List<int> ParsePorts(string input)
    {
        var result = new HashSet<int>();
        var tokens = input.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var token in tokens)
        {
            if (token.Contains('-'))
            {
                var range = token.Split('-');
                if (range.Length == 2 && int.TryParse(range[0], out var start) && int.TryParse(range[1], out var end))
                {
                    for (int p = Math.Min(start, end); p <= Math.Min(Math.Max(start, end), Math.Min(start, end) + 50); p++)
                    {
                        if (p is > 0 and <= 65535) result.Add(p);
                    }
                }
            }
            else if (int.TryParse(token, out var singlePort))
            {
                if (singlePort is > 0 and <= 65535) result.Add(singlePort);
            }
        }

        return result.OrderBy(p => p).ToList();
    }
}
