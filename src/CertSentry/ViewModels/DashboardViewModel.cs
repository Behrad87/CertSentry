using System;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CertSentry.Enums;
using CertSentry.Models;
using CertSentry.Services;
using Wpf.Ui.Abstractions.Controls;

namespace CertSentry.ViewModels;

public partial class DashboardViewModel : ObservableObject, INavigationAware
{
    private readonly ISystemDoctorService _doctorService;
    private readonly IAspNetCoreDevCertDoctor _aspNetDoctor;
    private readonly ICertificateStoreService _storeService;

    [ObservableProperty]
    private SystemHealthReport _healthReport = new();

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private string _actionMessage = string.Empty;

    public DashboardViewModel(
        ISystemDoctorService doctorService,
        IAspNetCoreDevCertDoctor aspNetDoctor,
        ICertificateStoreService storeService)
    {
        _doctorService = doctorService;
        _aspNetDoctor = aspNetDoctor;
        _storeService = storeService;
    }

    public void OnNavigatedTo()
    {
        _ = RefreshHealthAsync();
    }

    public Task OnNavigatedToAsync() => RefreshHealthAsync();

    public void OnNavigatedFrom() { }
    public Task OnNavigatedFromAsync() => Task.CompletedTask;

    [RelayCommand]
    public async Task RefreshHealthAsync()
    {
        IsBusy = true;
        StatusMessage = "Analyzing system localhost certificates and configuration...";
        try
        {
            HealthReport = await _doctorService.GenerateHealthReportAsync();
            StatusMessage = $"Health check completed: Score {HealthReport.HealthScore}/100 ({HealthReport.HealthLevel})";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error checking health: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task RepairDevCertsAsync()
    {
        IsBusy = true;
        ActionMessage = "Starting 1-Click Repair on ASP.NET Core dev-certs...";
        try
        {
            await _aspNetDoctor.RunCleanAndReissueAsync(msg => ActionMessage = msg);
            await RefreshHealthAsync();
            ActionMessage = "1-Click Repair completed successfully! Certificates refreshed.";
        }
        catch (Exception ex)
        {
            ActionMessage = $"Repair failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task PurgeStaleCertsAsync()
    {
        IsBusy = true;
        ActionMessage = "Scanning for expired localhost certificates to purge...";
        try
        {
            var devCerts = await _storeService.GetAllDevCertificatesAsync(includeLocalMachine: false);
            var expired = devCerts.Where(c => c.HealthStatus == CertificateHealthStatus.Expired).ToList();

            int removed = 0;
            foreach (var cert in expired)
            {
                if (Enum.TryParse<StoreName>(cert.StoreName, out var sName))
                {
                    var ok = await _storeService.DeleteCertificateAsync(cert.Thumbprint, sName, cert.StoreLocation);
                    if (ok) removed++;
                }
            }

            await RefreshHealthAsync();
            ActionMessage = $"Purged {removed} stale/expired certificates from CurrentUser store.";
        }
        catch (Exception ex)
        {
            ActionMessage = $"Purge failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
