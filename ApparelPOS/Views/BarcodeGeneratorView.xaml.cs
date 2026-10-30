using System.Windows.Controls;

namespace ApparelPOS.Views;

public partial class BarcodeGeneratorView : UserControl
{
    public BarcodeGeneratorView()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            if (DataContext is ViewModels.BarcodeGeneratorViewModel vm)
            {
                await vm.RefreshDataAsync();
            }
        };
    }
}
