using System;
using Wpf.Ui.Abstractions;

namespace CertSentry.Services;

/// <summary>
/// Provides pages for WPF-UI NavigationView based on Dependency Injection.
/// </summary>
public class PageService : INavigationViewPageProvider
{
    private readonly IServiceProvider _serviceProvider;

    public PageService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public object? GetPage(Type pageType)
    {
        return _serviceProvider.GetService(pageType);
    }
}
