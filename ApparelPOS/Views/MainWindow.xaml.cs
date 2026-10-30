using System.Windows;
using System.Windows.Input;
using ApparelPOS.ViewModels;

namespace ApparelPOS.Views;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel mainVm) return;

        // F1: New Bill (Switch to billing if not active)
        if (e.Key == Key.F1)
        {
            mainVm.NavigateToBillingCommand.Execute(null);
            mainVm.BillingVm.NewBillCommand.Execute(null);
            e.Handled = true;
            return;
        }

        // F2: Focus Barcode Box
        if (e.Key == Key.F2)
        {
            mainVm.NavigateToBillingCommand.Execute(null);
            e.Handled = true;
            return;
        }

        // F3: Manual Item Picker (Apparel Catalog)
        if (e.Key == Key.F3)
        {
            mainVm.NavigateToBillingCommand.Execute(null);
            mainVm.BillingVm.OpenCatalogModalCommand.Execute(null);
            e.Handled = true;
            return;
        }

        // F4: Laptop Camera Barcode Scanner
        if (e.Key == Key.F4)
        {
            mainVm.NavigateToBillingCommand.Execute(null);
            mainVm.BillingVm.ToggleCameraScannerCommand.Execute(null);
            e.Handled = true;
            return;
        }

        // Only handle F8, F9, Escape on Billing view
        if (mainVm.IsBillingActive)
        {
            if (e.Key == Key.F8)
            {
                mainVm.BillingVm.ToggleUpiModalCommand.Execute(null);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.F9)
            {
                mainVm.BillingVm.CheckoutAndPrintCommand.Execute(null);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Escape)
            {
                if (mainVm.BillingVm.IsCameraScannerOpen)
                {
                    mainVm.BillingVm.CloseCameraScannerCommand.Execute(null);
                    e.Handled = true;
                    return;
                }
                if (mainVm.BillingVm.IsCatalogModalOpen)
                {
                    mainVm.BillingVm.IsCatalogModalOpen = false;
                    e.Handled = true;
                    return;
                }
                if (mainVm.BillingVm.IsUpiModalOpen)
                {
                    mainVm.BillingVm.IsUpiModalOpen = false;
                    e.Handled = true;
                    return;
                }
                if (mainVm.BillingVm.IsReceiptPreviewOpen)
                {
                    mainVm.BillingVm.IsReceiptPreviewOpen = false;
                    e.Handled = true;
                    return;
                }
            }
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        if (DataContext is MainViewModel mainVm)
        {
            mainVm.BillingVm.CloseCameraScanner();
        }
    }
}
