using System.Windows.Controls;
using CertSentry.ViewModels;
using Wpf.Ui.Abstractions.Controls;

namespace CertSentry.Views.Pages;

public partial class SettingsPage : Page, INavigableView<SettingsViewModel>
{
    public SettingsViewModel ViewModel { get; }

    public SettingsPage(SettingsViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;
        InitializeComponent();
    }
}
