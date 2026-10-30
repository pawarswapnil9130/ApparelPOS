using System.Windows;
using System.Windows.Controls;
using ApparelPOS.ViewModels;

namespace ApparelPOS.Views;

public partial class InventoryView : UserControl
{
    public InventoryView()
    {
        InitializeComponent();
    }

    private void CloseProductModal_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is InventoryViewModel vm)
        {
            vm.IsEditProductModalOpen = false;
        }
    }

    private void CloseVariantModal_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is InventoryViewModel vm)
        {
            vm.IsAddVariantModalOpen = false;
        }
    }

    private void CloseStockModal_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is InventoryViewModel vm)
        {
            vm.IsAddStockModalOpen = false;
        }
    }
}
