using System.Windows.Controls;
using CertSentry.ViewModels;
using Wpf.Ui.Abstractions.Controls;

namespace CertSentry.Views.Pages;

public partial class PortScannerPage : Page, INavigableView<PortScannerViewModel>
{
    public PortScannerViewModel ViewModel { get; }

    public PortScannerPage(PortScannerViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;
        InitializeComponent();
    }
}
