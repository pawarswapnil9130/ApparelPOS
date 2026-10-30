using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ApparelPOS.ViewModels;

namespace ApparelPOS.Views;

public partial class BillingView : UserControl
{
    public BillingView()
    {
        InitializeComponent();
        Loaded += BillingView_Loaded;
        PreviewKeyDown += BillingView_PreviewKeyDown;
    }

    private void BillingView_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not BillingViewModel vm) return;

        if (e.Key == Key.F3)
        {
            vm.OpenCatalogModalCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            if (vm.IsCatalogModalOpen)
            {
                vm.IsCatalogModalOpen = false;
                e.Handled = true;
            }
            else if (vm.IsReceiptPreviewOpen)
            {
                vm.IsReceiptPreviewOpen = false;
                e.Handled = true;
            }
        }
        else if (e.Key == Key.F1)
        {
            vm.NewBillCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.F8)
        {
            vm.ToggleUpiModalCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.F9)
        {
            vm.CheckoutAndPrintCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void BillingView_Loaded(object sender, RoutedEventArgs e)
    {
        FocusBarcodeScanner();
    }

    public void FocusBarcodeScanner()
    {
        TxtBarcode.Focus();
        TxtBarcode.SelectAll();
    }

    private void TxtBarcode_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (DataContext is BillingViewModel vm)
            {
                vm.ProcessScanCommand.Execute(null);
                FocusBarcodeScanner();
                e.Handled = true;
            }
        }
    }

    private void PayCash_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is BillingViewModel vm)
        {
            vm.PaymentMethod = "Cash";
        }
    }

    private void PayCard_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is BillingViewModel vm)
        {
            vm.PaymentMethod = "Card";
        }
    }

    private void PayUpi_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is BillingViewModel vm)
        {
            vm.PaymentMethod = "UPI";
        }
    }
}
