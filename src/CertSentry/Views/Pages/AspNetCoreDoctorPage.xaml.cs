using System.Windows.Controls;
using CertSentry.ViewModels;
using Wpf.Ui.Abstractions.Controls;

namespace CertSentry.Views.Pages;

public partial class AspNetCoreDoctorPage : Page, INavigableView<AspNetCoreDoctorViewModel>
{
    public AspNetCoreDoctorViewModel ViewModel { get; }

    public AspNetCoreDoctorPage(AspNetCoreDoctorViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;
        InitializeComponent();
    }
}
