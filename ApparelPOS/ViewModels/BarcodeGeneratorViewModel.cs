using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media.Imaging;
using ApparelPOS.Models;
using ApparelPOS.Services;
using CommunityToolkit.Mvvm.Input;

namespace ApparelPOS.ViewModels;

public class BarcodeGeneratorViewModel : ViewModelBase
{
    private readonly IInventoryService _inventoryService;
    private readonly IBarcodeService _barcodeService;
    private readonly IBillingService _billingService;
    private StoreSettings _storeSettings = new();

    public ObservableCollection<Product> Products { get; } = new();
    public ObservableCollection<ProductVariant> Variants { get; } = new();

    private Product? _selectedProduct;
    public Product? SelectedProduct
    {
        get => _selectedProduct;
        set
        {
            if (SetProperty(ref _selectedProduct, value))
            {
                OnProductSelected();
            }
        }
    }

    private ProductVariant? _selectedVariant;
    public ProductVariant? SelectedVariant
    {
        get => _selectedVariant;
        set
        {
            if (SetProperty(ref _selectedVariant, value))
            {
                UpdatePreview();
            }
        }
    }

    private int _printCopies = 1;
    public int PrintCopies
    {
        get => _printCopies;
        set => SetProperty(ref _printCopies, Math.Max(1, value));
    }

    private string _previewBarcode = string.Empty;
    public string PreviewBarcode
    {
        get => _previewBarcode;
        set => SetProperty(ref _previewBarcode, value);
    }

    private string _previewTitle = string.Empty;
    public string PreviewTitle
    {
        get => _previewTitle;
        set => SetProperty(ref _previewTitle, value);
    }

    private string _previewDetails = string.Empty;
    public string PreviewDetails
    {
        get => _previewDetails;
        set => SetProperty(ref _previewDetails, value);
    }

    private string _previewPrice = string.Empty;
    public string PreviewPrice
    {
        get => _previewPrice;
        set => SetProperty(ref _previewPrice, value);
    }

    private BitmapSource? _barcodeImageSource;
    public BitmapSource? BarcodeImageSource
    {
        get => _barcodeImageSource;
        set => SetProperty(ref _barcodeImageSource, value);
    }

    private string _tsplScript = string.Empty;
    public string TsplScript
    {
        get => _tsplScript;
        set => SetProperty(ref _tsplScript, value);
    }

    public string StoreName => _storeSettings.StoreName;

    // Commands
    public IRelayCommand PrintLabelCommand { get; }
    public IRelayCommand OpenBrowserPrintPreviewCommand { get; }
    public IRelayCommand CopyTsplCommand { get; }
    public IAsyncRelayCommand RefreshProductsCommand { get; }

    public BarcodeGeneratorViewModel(
        IInventoryService inventoryService, 
        IBarcodeService barcodeService, 
        IBillingService billingService)
    {
        _inventoryService = inventoryService;
        _barcodeService = barcodeService;
        _billingService = billingService;

        PrintLabelCommand = new RelayCommand(PrintLabel);
        OpenBrowserPrintPreviewCommand = new RelayCommand(OpenBrowserPrintPreview);
        CopyTsplCommand = new RelayCommand(CopyTspl);
        RefreshProductsCommand = new AsyncRelayCommand(RefreshDataAsync);

        _ = RefreshDataAsync();
    }

    public async Task RefreshDataAsync()
    {
        var currentProdId = SelectedProduct?.Id;
        _storeSettings = await _billingService.GetStoreSettingsAsync();
        OnPropertyChanged(nameof(StoreName));

        var list = await _inventoryService.GetAllProductsAsync();
        Products.Clear();
        foreach (var p in list)
        {
            Products.Add(p);
        }

        if (currentProdId.HasValue)
        {
            SelectedProduct = Products.FirstOrDefault(p => p.Id == currentProdId.Value) ?? Products.FirstOrDefault();
        }
        else if (Products.Any())
        {
            SelectedProduct = Products.First();
        }
    }

    private void OnProductSelected()
    {
        Variants.Clear();
        if (SelectedProduct != null)
        {
            if (SelectedProduct.Variants != null && SelectedProduct.Variants.Any())
            {
                foreach (var v in SelectedProduct.Variants)
                {
                    Variants.Add(v);
                }
            }
            else
            {
                // Fallback virtual variant so single-size items can still print barcode labels
                Variants.Add(new ProductVariant
                {
                    ProductId = SelectedProduct.Id,
                    Size = "Standard / Free Size",
                    Color = "Default",
                    SkuVariant = SelectedProduct.Sku,
                    BarcodeVariant = SelectedProduct.Barcode,
                    StockQuantity = SelectedProduct.TotalStock
                });
            }
            SelectedVariant = Variants.FirstOrDefault();
        }
        else
        {
            SelectedVariant = null;
        }

        UpdatePreview();
    }

    private void UpdatePreview()
    {
        if (SelectedProduct == null)
        {
            PreviewTitle = "No product selected";
            PreviewDetails = "";
            PreviewPrice = "";
            PreviewBarcode = "";
            BarcodeImageSource = null;
            TsplScript = "";
            return;
        }

        PreviewTitle = SelectedProduct.Name;
        var barcode = SelectedVariant != null && !string.IsNullOrWhiteSpace(SelectedVariant.BarcodeVariant)
            ? SelectedVariant.BarcodeVariant
            : SelectedProduct.Barcode;

        PreviewBarcode = barcode;
        var price = SelectedVariant != null && SelectedVariant.EffectivePrice > 0
            ? SelectedVariant.EffectivePrice
            : SelectedProduct.SellingPrice;

        PreviewPrice = $"MRP: {_storeSettings.CurrencySymbol}{price:N2} (Incl. Taxes)";

        if (SelectedVariant != null)
        {
            PreviewDetails = $"SZ: {SelectedVariant.Size} | {SelectedVariant.Color} ({SelectedVariant.FitType})";
        }
        else
        {
            PreviewDetails = $"HSN: {SelectedProduct.Category}";
        }

        // Render barcode image
        BarcodeImageSource = _barcodeService.GenerateBarcodeImageSource(barcode, 320, 90);

        // Generate TSPL script
        if (SelectedVariant != null)
        {
            TsplScript = _barcodeService.GenerateTsplCommands(SelectedProduct, SelectedVariant, _storeSettings, PrintCopies);
        }
    }

    private void PrintLabel()
    {
        if (SelectedProduct == null || SelectedVariant == null)
        {
            StatusMessage = "Please select a product and variant to print.";
            return;
        }

        try
        {
            bool printed = _barcodeService.PrintLabel(SelectedProduct, SelectedVariant, _storeSettings, PrintCopies, _storeSettings.LabelPrinterName, forceDialog: true);
            if (printed)
            {
                StatusMessage = $"Sent {PrintCopies} label(s) to printer.";
            }
            else
            {
                StatusMessage = "Print was cancelled in the print dialog.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Print dialog notice: {ex.Message}";
        }
    }

    private void OpenBrowserPrintPreview()
    {
        if (SelectedProduct == null || SelectedVariant == null)
        {
            StatusMessage = "Please select a product and variant to preview/print.";
            return;
        }

        try
        {
            var html = _barcodeService.GenerateHtmlLabelPreview(SelectedProduct, SelectedVariant, _storeSettings, PrintCopies);
            var tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"ApparelPOS_Labels_{DateTime.Now:yyyyMMddHHmmss}.html");
            System.IO.File.WriteAllText(tempPath, html);

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = tempPath,
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(psi);

            StatusMessage = $"Opened {PrintCopies} label(s) in Edge/Browser with Print Dialog ready.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Preview error: {ex.Message}";
        }
    }

    private void CopyTspl()
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(TsplScript))
            {
                Clipboard.SetText(TsplScript);
                StatusMessage = "TSPL commands copied to clipboard.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Copy failed: {ex.Message}";
        }
    }
}
