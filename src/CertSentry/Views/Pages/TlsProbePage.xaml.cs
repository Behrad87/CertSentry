using System.Windows.Controls;
using CertSentry.ViewModels;
using Wpf.Ui.Abstractions.Controls;

namespace CertSentry.Views.Pages;

public partial class TlsProbePage : Page, INavigableView<TlsProbeViewModel>
{
    public TlsProbeViewModel ViewModel { get; }

    public TlsProbePage(TlsProbeViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;
        InitializeComponent();
    }
}
