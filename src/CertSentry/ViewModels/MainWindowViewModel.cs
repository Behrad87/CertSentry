using CommunityToolkit.Mvvm.ComponentModel;

namespace CertSentry.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private string _applicationTitle = "CertSentry — Localhost SSL/TLS Doctor";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = "Ready";
}
