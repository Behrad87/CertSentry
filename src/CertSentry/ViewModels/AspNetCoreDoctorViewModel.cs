using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using CertSentry.Models;
using CertSentry.Services;
using Wpf.Ui.Abstractions.Controls;

namespace CertSentry.ViewModels;

public partial class AspNetCoreDoctorViewModel : ObservableObject, INavigationAware
{
    private readonly IAspNetCoreDevCertDoctor _doctor;

    [ObservableProperty]
    private AspNetDevCertDiagnosis _diagnosis = new();

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _terminalOutput = "Waiting for analysis...";

    [ObservableProperty]
    private string _pfxPassword = string.Empty;

    public AspNetCoreDoctorViewModel(IAspNetCoreDevCertDoctor doctor)
    {
        _doctor = doctor;
    }

    public void OnNavigatedTo()
    {
        _ = DiagnoseAsync();
    }

    public Task OnNavigatedToAsync() => DiagnoseAsync();
    public void OnNavigatedFrom() { }
    public Task OnNavigatedFromAsync() => Task.CompletedTask;

    [RelayCommand]
    public async Task DiagnoseAsync()
    {
        IsBusy = true;
        AppendTerminal("Running: dotnet dev-certs https --check-trust-machine-readable");
        try
        {
            Diagnosis = await _doctor.DiagnoseAsync();
            AppendTerminal($"Analysis result: {Diagnosis.SummaryMessage}");
            if (Diagnosis.HasDuplicates)
            {
                AppendTerminal($"WARNING: Detected {Diagnosis.Certificates.Count} conflicting versions in certificate stores!");
            }
        }
        catch (Exception ex)
        {
            AppendTerminal($"Error: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task CleanAndReissueAsync()
    {
        IsBusy = true;
        AppendTerminal("=== Starting 1-Click ASP.NET Core Dev-Cert Repair ===");
        try
        {
            await _doctor.RunCleanAndReissueAsync(msg => AppendTerminal(msg));
            await DiagnoseAsync();
        }
        catch (Exception ex)
        {
            AppendTerminal($"Repair failed: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task TrustCertAsync()
    {
        IsBusy = true;
        AppendTerminal("Running: dotnet dev-certs https --trust");
        try
        {
            var output = await _doctor.RunTrustAsync();
            AppendTerminal(output);
            await DiagnoseAsync();
        }
        catch (Exception ex)
        {
            AppendTerminal($"Trust failed: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task CleanCertsAsync()
    {
        var confirm = MessageBox.Show(
            "This will remove ALL ASP.NET Core HTTPS developer certificates from the machine ('dotnet dev-certs https --clean').\n\nDo you want to proceed?",
            "Clean Developer Certificates",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes) return;

        IsBusy = true;
        AppendTerminal("Running: dotnet dev-certs https --clean");
        try
        {
            var output = await _doctor.RunCleanAsync();
            AppendTerminal(output);
            await DiagnoseAsync();
        }
        catch (Exception ex)
        {
            AppendTerminal($"Clean failed: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task ExportPfxAsync()
    {
        var sfd = new SaveFileDialog
        {
            FileName = "aspnet-dev-cert.pfx",
            Filter = "PKCS#12 PFX Bundle (*.pfx)|*.pfx"
        };

        if (sfd.ShowDialog() == true)
        {
            IsBusy = true;
            AppendTerminal($"Exporting certificate with private key to '{sfd.FileName}'...");
            try
            {
                var output = await _doctor.ExportPfxAsync(sfd.FileName, PfxPassword);
                AppendTerminal(output);
                AppendTerminal($"Export completed: {sfd.FileName}");
            }
            catch (Exception ex)
            {
                AppendTerminal($"Export error: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }
    }

    private void AppendTerminal(string message)
    {
        TerminalOutput = $"{TerminalOutput}\n[{DateTime.Now:HH:mm:ss}] {message}";
    }
}
