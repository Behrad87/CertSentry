using System.Windows.Controls;
using CertSentry.ViewModels;
using Wpf.Ui.Abstractions.Controls;

namespace CertSentry.Views.Pages;

public partial class StoreExplorerPage : Page, INavigableView<StoreExplorerViewModel>
{
    public StoreExplorerViewModel ViewModel { get; }

    public StoreExplorerPage(StoreExplorerViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;
        InitializeComponent();
    }
}
