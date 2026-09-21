using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Wpf.Ui.Abstractions.Controls;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace CertSentry.ViewModels;

public partial class SettingsViewModel : ObservableObject, INavigationAware
{
    [ObservableProperty]
    private string _currentTheme = "Dark";

    [ObservableProperty]
    private string _appVersion = "1.0.0 (Release)";

    [ObservableProperty]
    private string _dotnetRuntime = RuntimeInformation.FrameworkDescription;

    [ObservableProperty]
    private string _osVersion = $"{RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})";

    [ObservableProperty]
    private string _statusMessage = "All settings applied.";

    public void OnNavigatedTo() { }
    public Task OnNavigatedToAsync() => Task.CompletedTask;
    public void OnNavigatedFrom() { }
    public Task OnNavigatedFromAsync() => Task.CompletedTask;

    [RelayCommand]
    public void SetDarkTheme()
    {
        ApplicationThemeManager.Apply(ApplicationTheme.Dark);
        CurrentTheme = "Dark";
        StatusMessage = "Dark theme applied.";
    }

    [RelayCommand]
    public void SetLightTheme()
    {
        ApplicationThemeManager.Apply(ApplicationTheme.Light);
        CurrentTheme = "Light";
        StatusMessage = "Light theme applied.";
    }

    [RelayCommand]
    public void SetSystemTheme()
    {
        var sysTheme = ApplicationThemeManager.GetSystemTheme();
        var targetTheme = sysTheme == SystemTheme.Light ? ApplicationTheme.Light : ApplicationTheme.Dark;
        ApplicationThemeManager.Apply(targetTheme);
        CurrentTheme = "System";
        StatusMessage = "System theme applied.";
    }
}
