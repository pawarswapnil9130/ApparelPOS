using System.Windows;
using ApparelPOS.ViewModels;

namespace ApparelPOS.Views;

public partial class ActivationWindow : Window
{
    public ActivationWindow(ActivationViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        viewModel.ActivationSuccessful += () =>
        {
            DialogResult = true;
            Close();
        };
    }
}
