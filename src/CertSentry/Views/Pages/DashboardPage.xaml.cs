using System.Windows.Controls;
using CertSentry.ViewModels;
using Wpf.Ui.Abstractions.Controls;

namespace CertSentry.Views.Pages;

public partial class DashboardPage : Page, INavigableView<DashboardViewModel>
{
    public DashboardViewModel ViewModel { get; }

    public DashboardPage(DashboardViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;
        InitializeComponent();
    }
}
