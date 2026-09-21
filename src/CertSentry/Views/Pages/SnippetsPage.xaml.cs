using System.Windows.Controls;
using CertSentry.ViewModels;
using Wpf.Ui.Abstractions.Controls;

namespace CertSentry.Views.Pages;

public partial class SnippetsPage : Page, INavigableView<SnippetsViewModel>
{
    public SnippetsViewModel ViewModel { get; }

    public SnippetsPage(SnippetsViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;
        InitializeComponent();
    }
}
