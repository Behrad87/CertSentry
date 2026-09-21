using System.Windows.Controls;
using CertSentry.ViewModels;
using Wpf.Ui.Abstractions.Controls;

namespace CertSentry.Views.Pages;

public partial class CertGeneratorPage : Page, INavigableView<CertGeneratorViewModel>
{
    public CertGeneratorViewModel ViewModel { get; }

    public CertGeneratorPage(CertGeneratorViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;
        InitializeComponent();
    }
}
