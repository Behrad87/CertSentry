using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using CertSentry.Services;
using Wpf.Ui.Abstractions.Controls;

namespace CertSentry.ViewModels;

public partial class SnippetsViewModel : ObservableObject, INavigationAware
{
    private readonly IEnvironmentSnippetService _snippetService;

    [ObservableProperty]
    private string _caCertPath = "C:\\certs\\localhost-ca.crt";

    [ObservableProperty]
    private string _privateKeyPath = "C:\\certs\\localhost-ca.key";

    [ObservableProperty]
    private int _portNumber = 5001;

    [ObservableProperty]
    private ObservableCollection<SnippetItem> _snippets = new();

    [ObservableProperty]
    private string _notificationMessage = string.Empty;

    public SnippetsViewModel(IEnvironmentSnippetService snippetService)
    {
        _snippetService = snippetService;
    }

    public void OnNavigatedTo()
    {
        RegenerateSnippets();
    }

    public Task OnNavigatedToAsync()
    {
        RegenerateSnippets();
        return Task.CompletedTask;
    }

    public void OnNavigatedFrom() { }
    public Task OnNavigatedFromAsync() => Task.CompletedTask;

    partial void OnCaCertPathChanged(string value) => RegenerateSnippets();
    partial void OnPrivateKeyPathChanged(string value) => RegenerateSnippets();
    partial void OnPortNumberChanged(int value) => RegenerateSnippets();

    [RelayCommand]
    public void RegenerateSnippets()
    {
        var list = _snippetService.GenerateSnippets(CaCertPath, PrivateKeyPath, PortNumber);
        Snippets = new ObservableCollection<SnippetItem>(list);
    }

    [RelayCommand]
    public void BrowseCaCert()
    {
        var ofd = new OpenFileDialog
        {
            Filter = "Certificate Files (*.crt;*.cer;*.pem)|*.crt;*.cer;*.pem|All files (*.*)|*.*",
            Title = "Select Root Certificate Authority File"
        };
        if (ofd.ShowDialog() == true)
        {
            CaCertPath = ofd.FileName;
        }
    }

    [RelayCommand]
    public void BrowsePrivateKey()
    {
        var ofd = new OpenFileDialog
        {
            Filter = "Key Files (*.key;*.pem)|*.key;*.pem|All files (*.*)|*.*",
            Title = "Select Private Key File"
        };
        if (ofd.ShowDialog() == true)
        {
            PrivateKeyPath = ofd.FileName;
        }
    }

    [RelayCommand]
    public void CopySnippet(SnippetItem? item)
    {
        if (item != null)
        {
            Clipboard.SetText(item.Code);
            NotificationMessage = $"Copied '{item.Title}' snippet to clipboard!";
        }
    }
}
