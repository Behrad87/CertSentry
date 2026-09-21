using System;
using System.IO;
using System.Reflection;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using CertSentry.Services;
using CertSentry.ViewModels;
using CertSentry.Views;
using CertSentry.Views.Pages;
using Wpf.Ui;
using Wpf.Ui.Abstractions;

namespace CertSentry;

public partial class App : Application
{
    private static readonly IHost _host = Host
        .CreateDefaultBuilder()
        .ConfigureServices((context, services) =>
        {
            // Core Services
            services.AddSingleton<ICertificateStoreService, CertificateStoreService>();
            services.AddSingleton<ITlsProbeService, TlsProbeService>();
            services.AddSingleton<ICertificateGeneratorService, CertificateGeneratorService>();
            services.AddSingleton<IAspNetCoreDevCertDoctor, AspNetCoreDevCertDoctor>();
            services.AddSingleton<IPortScannerService, PortScannerService>();
            services.AddSingleton<IEnvironmentSnippetService, EnvironmentSnippetService>();
            services.AddSingleton<ISystemDoctorService, SystemDoctorService>();

            // Navigation & UI Services
            services.AddSingleton<INavigationViewPageProvider, PageService>();
            services.AddSingleton<INavigationService, NavigationService>();
            services.AddSingleton<ISnackbarService, SnackbarService>();

            // ViewModels
            services.AddSingleton<MainWindowViewModel>();
            services.AddSingleton<DashboardViewModel>();
            services.AddSingleton<TlsProbeViewModel>();
            services.AddSingleton<StoreExplorerViewModel>();
            services.AddSingleton<CertGeneratorViewModel>();
            services.AddSingleton<AspNetCoreDoctorViewModel>();
            services.AddSingleton<PortScannerViewModel>();
            services.AddSingleton<SnippetsViewModel>();
            services.AddSingleton<SettingsViewModel>();

            // Views & Pages
            services.AddSingleton<MainWindow>();
            services.AddSingleton<DashboardPage>();
            services.AddSingleton<TlsProbePage>();
            services.AddSingleton<StoreExplorerPage>();
            services.AddSingleton<CertGeneratorPage>();
            services.AddSingleton<AspNetCoreDoctorPage>();
            services.AddSingleton<PortScannerPage>();
            services.AddSingleton<SnippetsPage>();
            services.AddSingleton<SettingsPage>();
        })
        .Build();

    public static T GetService<T>() where T : class
    {
        return _host.Services.GetRequiredService<T>();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        await _host.StartAsync();

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();

        var navigationService = _host.Services.GetRequiredService<INavigationService>();
        navigationService.Navigate(typeof(DashboardPage));
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        await _host.StopAsync();
        _host.Dispose();
        base.OnExit(e);
    }
}
