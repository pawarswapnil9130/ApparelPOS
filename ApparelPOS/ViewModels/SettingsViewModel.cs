using System.Collections.ObjectModel;
using ApparelPOS.Data;
using ApparelPOS.Models;
using ApparelPOS.Services;
using CommunityToolkit.Mvvm.Input;

namespace ApparelPOS.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private readonly IBillingService _billingService;
    private readonly IPrinterService _printerService;
    private readonly IBarcodeService _barcodeService;
    private readonly ILicenseService _licenseService;
    private readonly IHardwareInfoService _hardwareInfoService;

    private StoreSettings _settings = new();
    public StoreSettings Settings
    {
        get => _settings;
        set => SetProperty(ref _settings, value);
    }

    public ObservableCollection<string> InstalledPrinters { get; } = new();

    public string MachineCode { get; }
    public LicenseInfo LicenseInfo { get; }

    public IAsyncRelayCommand SaveSettingsCommand { get; }
    public IRelayCommand TestThermalPrintCommand { get; }
    public IAsyncRelayCommand RefreshPrintersCommand { get; }
    public IAsyncRelayCommand RestoreSampleCatalogCommand { get; }

    public SettingsViewModel(
        IBillingService billingService,
        IPrinterService printerService,
        IBarcodeService barcodeService,
        ILicenseService licenseService,
        IHardwareInfoService hardwareInfoService)
    {
        _billingService = billingService;
        _printerService = printerService;
        _barcodeService = barcodeService;
        _licenseService = licenseService;
        _hardwareInfoService = hardwareInfoService;

        MachineCode = _hardwareInfoService.GetMachineCode();
        LicenseInfo = _licenseService.GetCachedLicenseInfo();

        SaveSettingsCommand = new AsyncRelayCommand(SaveSettingsAsync);
        TestThermalPrintCommand = new RelayCommand(TestThermalPrint);
        RefreshPrintersCommand = new AsyncRelayCommand(LoadPrintersAndSettingsAsync);
        RestoreSampleCatalogCommand = new AsyncRelayCommand(RestoreSampleCatalogAsync);

        _ = LoadPrintersAndSettingsAsync();
    }

    private async Task RestoreSampleCatalogAsync()
    {
        try
        {
            using var db = new AppDbContext();
            DbInitializer.SeedSampleProducts(db);
            StatusMessage = "Successfully restored sample garments and 24 size variants! (Existing items kept intact)";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Restore notice: {ex.Message}";
        }
    }

    private async Task LoadPrintersAndSettingsAsync()
    {
        var printers = _printerService.GetInstalledPrinters();
        InstalledPrinters.Clear();
        foreach (var p in printers)
        {
            InstalledPrinters.Add(p);
        }

        Settings = await _billingService.GetStoreSettingsAsync();
        if (string.IsNullOrWhiteSpace(Settings.ThermalPrinterName) || !InstalledPrinters.Contains(Settings.ThermalPrinterName))
        {
            Settings.ThermalPrinterName = PrinterService.NonePrinterOption;
        }
        if (string.IsNullOrWhiteSpace(Settings.LabelPrinterName) || !InstalledPrinters.Contains(Settings.LabelPrinterName))
        {
            Settings.LabelPrinterName = PrinterService.NonePrinterOption;
        }
    }

    private async Task SaveSettingsAsync()
    {
        IsBusy = true;
        try
        {
            await _billingService.SaveStoreSettingsAsync(Settings);
            StatusMessage = "Settings saved successfully.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to save settings: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void TestThermalPrint()
    {
        try
        {
            var dummyInvoice = new Invoice
            {
                InvoiceNumber = "TEST-PRINT-001",
                InvoiceDate = DateTime.Now,
                CustomerName = "Printer Diagnostics",
                CustomerPhone = "0000000000",
                PaymentMethod = "Cash",
                SubTotal = 1499.00m,
                TotalAmount = 1499.00m,
                CashReceived = 1500.00m,
                ChangeDue = 1.00m,
                Items = new List<InvoiceItem>
                {
                    new()
                    {
                        ItemName = "Sample Apparel Test Item",
                        Size = "40 (M)",
                        Color = "Navy",
                        Quantity = 1,
                        UnitPrice = 1499.00m,
                        TotalPrice = 1499.00m
                    }
                }
            };

            _printerService.PrintReceipt(dummyInvoice, Settings);
            StatusMessage = "Test receipt dispatched to printer!";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Printer test notice: {ex.Message}";
        }
    }
}
