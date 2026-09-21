using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using CertSentry.Enums;
using CertSentry.Services;
using Wpf.Ui.Abstractions.Controls;

namespace CertSentry.ViewModels;

public partial class CertGeneratorViewModel : ObservableObject, INavigationAware
{
    private readonly ICertificateGeneratorService _generatorService;
    private readonly ICertificateStoreService _storeService;
    private X509Certificate2? _lastGeneratedCert;

    [ObservableProperty]
    private string _commonName = "localhost";

    [ObservableProperty]
    private string _sanInput = "localhost, 127.0.0.1, ::1, host.docker.internal, *.dev.localhost";

    [ObservableProperty]
    private int _selectedAlgorithmIndex = 0; // 0=RSA 2048, 1=RSA 4096, 2=ECDSA P-256

    [ObservableProperty]
    private int _validityDays = 365;

    [ObservableProperty]
    private bool _isCertificateAuthority = false;

    [ObservableProperty]
    private bool _autoInstallToStore = true;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = "Ready to generate custom certificates.";

    [ObservableProperty]
    private string _certificatePemOutput = string.Empty;

    [ObservableProperty]
    private string _privateKeyPemOutput = string.Empty;

    [ObservableProperty]
    private bool _hasGeneratedCertificate;

    [ObservableProperty]
    private string _notificationMessage = string.Empty;

    public CertGeneratorViewModel(
        ICertificateGeneratorService generatorService,
        ICertificateStoreService storeService)
    {
        _generatorService = generatorService;
        _storeService = storeService;
    }

    public void OnNavigatedTo() { }
    public Task OnNavigatedToAsync() => Task.CompletedTask;
    public void OnNavigatedFrom() { }
    public Task OnNavigatedFromAsync() => Task.CompletedTask;

    [RelayCommand]
    public async Task GenerateCertificateAsync()
    {
        if (string.IsNullOrWhiteSpace(CommonName))
        {
            NotificationMessage = "Common Name (CN) is required.";
            return;
        }

        IsBusy = true;
        NotificationMessage = string.Empty;
        StatusMessage = "Generating cryptographic key pair and X.509 certificate...";

        try
        {
            var sans = SanInput.Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();

            var algo = SelectedAlgorithmIndex switch
            {
                1 => KeyAlgorithmType.Rsa4096,
                2 => KeyAlgorithmType.EcdsaP256,
                _ => KeyAlgorithmType.Rsa2048
            };

            var req = new CertificateGenerationRequest
            {
                CommonName = CommonName.Trim(),
                SubjectAlternativeNames = sans,
                KeyAlgorithm = algo,
                ValidityDays = ValidityDays,
                IsCertificateAuthority = IsCertificateAuthority
            };

            var cert = await Task.Run(() => _generatorService.GenerateCertificate(req));
            _lastGeneratedCert = cert;

            var (certPem, keyPem) = _generatorService.ExportToPem(cert);
            CertificatePemOutput = certPem;
            PrivateKeyPemOutput = keyPem;
            HasGeneratedCertificate = true;

            string installStatus = string.Empty;
            if (AutoInstallToStore)
            {
                StatusMessage = "Installing into Windows Certificate Store...";
                if (IsCertificateAuthority)
                {
                    await _storeService.InstallCertificateAsync(cert, StoreName.Root, StoreLocation.CurrentUser);
                    installStatus = " and installed into CurrentUser\\Root (Trusted Root CA).";
                }
                else
                {
                    await _storeService.InstallCertificateAsync(cert, StoreName.My, StoreLocation.CurrentUser);
                    installStatus = " and installed into CurrentUser\\My (Personal).";
                }
            }

            StatusMessage = $"Successfully generated certificate for '{CommonName}'{installStatus}";
            NotificationMessage = "Certificate created successfully!";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Generation failed: {ex.Message}";
            NotificationMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task ExportPfxAsync()
    {
        if (_lastGeneratedCert == null) return;

        var sfd = new SaveFileDialog
        {
            FileName = $"{CommonName.Replace("*", "wildcard")}.pfx",
            Filter = "PKCS#12 PFX Bundle (*.pfx)|*.pfx"
        };

        if (sfd.ShowDialog() == true)
        {
            var pfxBytes = _generatorService.ExportToPfx(_lastGeneratedCert, "");
            await File.WriteAllBytesAsync(sfd.FileName, pfxBytes);
            NotificationMessage = $"Saved PFX file to {Path.GetFileName(sfd.FileName)}";
        }
    }

    [RelayCommand]
    public async Task ExportPemPairAsync()
    {
        if (_lastGeneratedCert == null) return;

        var sfd = new SaveFileDialog
        {
            FileName = $"{CommonName.Replace("*", "wildcard")}.crt",
            Filter = "Certificate PEM (*.crt)|*.crt"
        };

        if (sfd.ShowDialog() == true)
        {
            var certPath = sfd.FileName;
            var keyPath = Path.ChangeExtension(certPath, ".key");

            await File.WriteAllTextAsync(certPath, CertificatePemOutput);
            await File.WriteAllTextAsync(keyPath, PrivateKeyPemOutput);
            NotificationMessage = $"Saved CRT ({Path.GetFileName(certPath)}) and KEY ({Path.GetFileName(keyPath)})";
        }
    }

    [RelayCommand]
    public void CopyCertPem()
    {
        if (!string.IsNullOrEmpty(CertificatePemOutput))
        {
            Clipboard.SetText(CertificatePemOutput);
            NotificationMessage = "Public certificate PEM copied to clipboard.";
        }
    }

    [RelayCommand]
    public void CopyKeyPem()
    {
        if (!string.IsNullOrEmpty(PrivateKeyPemOutput))
        {
            Clipboard.SetText(PrivateKeyPemOutput);
            NotificationMessage = "Private key PEM copied to clipboard.";
        }
    }
}
