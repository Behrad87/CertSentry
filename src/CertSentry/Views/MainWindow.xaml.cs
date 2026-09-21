using System;
using CertSentry.ViewModels;
using Wpf.Ui;
using Wpf.Ui.Abstractions;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace CertSentry.Views;

public partial class MainWindow : FluentWindow, INavigationWindow
{
    public MainWindowViewModel ViewModel { get; }

    public MainWindow(
        MainWindowViewModel viewModel,
        INavigationViewPageProvider pageProvider,
        INavigationService navigationService)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();

        SetPageService(pageProvider);
        navigationService.SetNavigationControl(RootNavigation);

        // Apply dark theme on launch
        ApplicationThemeManager.Apply(ApplicationTheme.Dark);
    }

    public INavigationView GetNavigation() => RootNavigation;

    public bool Navigate(Type pageType) => RootNavigation.Navigate(pageType);

    public void SetPageService(INavigationViewPageProvider navigationViewPageProvider)
    {
        RootNavigation.SetPageProviderService(navigationViewPageProvider);
    }

    public void SetServiceProvider(IServiceProvider serviceProvider) { }

    public void ShowWindow() => Show();

    public void CloseWindow() => Close();
}
