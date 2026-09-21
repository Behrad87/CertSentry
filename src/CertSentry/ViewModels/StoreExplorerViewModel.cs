using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using CertSentry.Models;
using CertSentry.Services;
using Wpf.Ui.Abstractions.Controls;

namespace CertSentry.ViewModels;

public partial class StoreExplorerViewModel : ObservableObject, INavigationAware
{
    private readonly ICertificateStoreService _storeService;
    private List<CertificateItem> _allLoadedCertificates = new();

    [ObservableProperty]
    private ObservableCollection<CertificateItem> _certificates = new();

    [ObservableProperty]
    private CertificateItem? _selectedCertificate;

    [ObservableProperty]
    private int _selectedStoreIndex = 0; // 0=User My, 1=User Root, 2=User CA, 3=Machine My, 4=Machine Root

    [ObservableProperty]
    private bool _devCertsOnly = true;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private string _notificationMessage = string.Empty;

    public StoreExplorerViewModel(ICertificateStoreService storeService)
    {
        _storeService = storeService;
    }

    public void OnNavigatedTo()
    {
        _ = LoadCertificatesAsync();
    }

    public Task OnNavigatedToAsync() => LoadCertificatesAsync();
    public void OnNavigatedFrom() { }
    public Task OnNavigatedFromAsync() => Task.CompletedTask;

    partial void OnSelectedStoreIndexChanged(int value) => _ = LoadCertificatesAsync();
    partial void OnDevCertsOnlyChanged(bool value) => ApplyFilter();
    partial void OnSearchTextChanged(string value) => ApplyFilter();

    [RelayCommand]
    public async Task LoadCertificatesAsync()
    {
        IsLoading = true;
        NotificationMessage = string.Empty;
        StatusMessage = "Reading Windows Certificate Store...";

        try
        {
            var (storeName, storeLocation) = GetSelectedStoreConfig();
            _allLoadedCertificates = await _storeService.GetCertificatesAsync(storeName, storeLocation, devCertsOnly: false);
            ApplyFilter();
            StatusMessage = $"Loaded {_allLoadedCertificates.Count} certificates from {storeLocation}\\{storeName}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load store: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyFilter()
    {
        var filtered = _allLoadedCertificates.AsEnumerable();

        if (DevCertsOnly)
        {
            filtered = filtered.Where(c => c.IsLocalhostCert);
        }

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var query = SearchText.Trim().ToLowerInvariant();
            filtered = filtered.Where(c =>
                c.Subject.ToLowerInvariant().Contains(query) ||
                c.Thumbprint.ToLowerInvariant().Contains(query) ||
                c.Issuer.ToLowerInvariant().Contains(query) ||
                c.SubjectAlternativeNames.Any(s => s.ToLowerInvariant().Contains(query)));
        }

        Certificates = new ObservableCollection<CertificateItem>(filtered.OrderBy(c => c.CommonName));
        SelectedCertificate = Certificates.FirstOrDefault();
    }

    private (StoreName Name, StoreLocation Location) GetSelectedStoreConfig()
    {
        return SelectedStoreIndex switch
        {
            1 => (StoreName.Root, StoreLocation.CurrentUser),
            2 => (StoreName.CertificateAuthority, StoreLocation.CurrentUser),
            3 => (StoreName.My, StoreLocation.LocalMachine),
            4 => (StoreName.Root, StoreLocation.LocalMachine),
            _ => (StoreName.My, StoreLocation.CurrentUser)
        };
    }

    [RelayCommand]
    public async Task DeleteSelectedCertificateAsync()
    {
        if (SelectedCertificate == null) return;

        var result = MessageBox.Show(
            $"Are you sure you want to delete the certificate for '{SelectedCertificate.CommonName}' (Thumbprint: {SelectedCertificate.Thumbprint})?\n\nThis action removes it from the Windows Certificate Store.",
            "Confirm Certificate Deletion",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        if (Enum.TryParse<StoreName>(SelectedCertificate.StoreName, out var sName))
        {
            var ok = await _storeService.DeleteCertificateAsync(SelectedCertificate.Thumbprint, sName, SelectedCertificate.StoreLocation);
            if (ok)
            {
                NotificationMessage = $"Deleted certificate '{SelectedCertificate.CommonName}'.";
                await LoadCertificatesAsync();
            }
            else
            {
                NotificationMessage = "Failed to delete certificate. Administrative elevation may be required.";
            }
        }
    }

    [RelayCommand]
    public async Task TrustSelectedCertificateAsync()
    {
        if (SelectedCertificate == null) return;

        var ok = await _storeService.TrustCertificateAsync(SelectedCertificate.Thumbprint, SelectedCertificate.StoreLocation);
        if (ok)
        {
            NotificationMessage = $"Certificate '{SelectedCertificate.CommonName}' added to Windows CurrentUser\\Root trust store.";
        }
        else
        {
            NotificationMessage = "Could not install certificate into root store.";
        }
    }

    [RelayCommand]
    public async Task ExportPemAsync()
    {
        if (SelectedCertificate == null) return;

        var sfd = new SaveFileDialog
        {
            FileName = $"{SelectedCertificate.CommonName.Replace("*", "wildcard")}.crt",
            Filter = "Certificate PEM (*.crt;*.pem)|*.crt;*.pem|All files (*.*)|*.*"
        };

        if (sfd.ShowDialog() == true)
        {
            if (Enum.TryParse<StoreName>(SelectedCertificate.StoreName, out var sName))
            {
                var bytes = await _storeService.ExportCertificateAsync(SelectedCertificate.Thumbprint, sName, SelectedCertificate.StoreLocation, asPfx: false);
                await File.WriteAllBytesAsync(sfd.FileName, bytes);
                NotificationMessage = $"Exported public certificate to {Path.GetFileName(sfd.FileName)}";
            }
        }
    }

    [RelayCommand]
    public async Task ExportPfxAsync()
    {
        if (SelectedCertificate == null) return;

        if (!SelectedCertificate.HasPrivateKey)
        {
            MessageBox.Show("This certificate does not possess an exportable private key on this machine.", "No Private Key", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var sfd = new SaveFileDialog
        {
            FileName = $"{SelectedCertificate.CommonName.Replace("*", "wildcard")}.pfx",
            Filter = "Personal Information Exchange (*.pfx)|*.pfx|All files (*.*)|*.*"
        };

        if (sfd.ShowDialog() == true)
        {
            if (Enum.TryParse<StoreName>(SelectedCertificate.StoreName, out var sName))
            {
                var bytes = await _storeService.ExportCertificateAsync(SelectedCertificate.Thumbprint, sName, SelectedCertificate.StoreLocation, asPfx: true, password: "");
                await File.WriteAllBytesAsync(sfd.FileName, bytes);
                NotificationMessage = $"Exported PFX bundle to {Path.GetFileName(sfd.FileName)}";
            }
        }
    }

    [RelayCommand]
    public void CopyThumbprint()
    {
        if (SelectedCertificate != null)
        {
            Clipboard.SetText(SelectedCertificate.Thumbprint);
            NotificationMessage = "Thumbprint copied to clipboard.";
        }
    }

    [RelayCommand]
    public void CopyPem()
    {
        if (SelectedCertificate != null)
        {
            Clipboard.SetText(SelectedCertificate.RawPem);
            NotificationMessage = "PEM certificate copied to clipboard.";
        }
    }
}
