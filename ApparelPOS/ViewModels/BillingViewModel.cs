using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media.Imaging;
using ApparelPOS.Models;
using ApparelPOS.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ApparelPOS.ViewModels;

public class CartItem : ObservableObject
{
    public int ProductId { get; set; }
    public int? VariantId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Barcode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public decimal CostPrice { get; set; }

    private int _quantity = 1;
    public int Quantity
    {
        get => _quantity;
        set
        {
            if (SetProperty(ref _quantity, Math.Max(1, value)))
            {
                Recalculate();
            }
        }
    }

    private decimal _unitPrice;
    public decimal UnitPrice
    {
        get => _unitPrice;
        set
        {
            if (SetProperty(ref _unitPrice, value))
            {
                Recalculate();
            }
        }
    }

    private decimal _discountPercent;
    public decimal DiscountPercent
    {
        get => _discountPercent;
        set
        {
            if (SetProperty(ref _discountPercent, Math.Clamp(value, 0m, 100m)))
            {
                Recalculate();
            }
        }
    }

    public decimal TaxRate { get; set; } = 5.0m;

    private decimal _discountAmount;
    public decimal DiscountAmount
    {
        get => _discountAmount;
        private set => SetProperty(ref _discountAmount, value);
    }

    private decimal _totalPrice;
    public decimal TotalPrice
    {
        get => _totalPrice;
        private set => SetProperty(ref _totalPrice, value);
    }

    public event Action? ItemChanged;

    public void Recalculate()
    {
        var gross = UnitPrice * Quantity;
        DiscountAmount = gross * (DiscountPercent / 100m);
        TotalPrice = gross - DiscountAmount;
        ItemChanged?.Invoke();
    }
}

public class CatalogPickerItem
{
    public Product Product { get; set; } = null!;
    public ProductVariant? Variant { get; set; }
    public string Name => Product.Name;
    public string Category => Product.Category;
    public string Size => Variant?.Size ?? "Free Size";
    public string Color => Variant?.Color ?? "Standard";
    public string Sku => Variant != null && !string.IsNullOrEmpty(Variant.SkuVariant) ? Variant.SkuVariant : Product.Sku;
    public string Barcode => Variant != null && !string.IsNullOrEmpty(Variant.BarcodeVariant) ? Variant.BarcodeVariant : Product.Barcode;
    public decimal Price => (Variant != null && Variant.EffectivePrice > 0) ? Variant.EffectivePrice : Product.SellingPrice;
    public int Stock => Variant?.StockQuantity ?? Product.TotalStock;
    public bool IsInStock => Stock > 0;
}

public class BillingViewModel : ViewModelBase
{
    private readonly IBillingService _billingService;
    private readonly IPrinterService _printerService;
    private readonly IBarcodeService _barcodeService;
    private readonly IInventoryService _inventoryService;
    private readonly ICameraScannerService? _cameraScannerService;
    private StoreSettings _storeSettings = new();

    public ObservableCollection<CartItem> CartItems { get; } = new();

    // Catalog Browser (Manual Add Items without Barcode Reader)
    private readonly List<CatalogPickerItem> _allCatalogItems = new();
    public ObservableCollection<CatalogPickerItem> FilteredCatalogItems { get; } = new();
    public ObservableCollection<string> CatalogCategories { get; } = new();

    private string _catalogSearchQuery = string.Empty;
    public string CatalogSearchQuery
    {
        get => _catalogSearchQuery;
        set
        {
            if (SetProperty(ref _catalogSearchQuery, value))
            {
                ApplyCatalogFilter();
            }
        }
    }

    private string _selectedCatalogCategory = "All Categories";
    public string SelectedCatalogCategory
    {
        get => _selectedCatalogCategory;
        set
        {
            if (SetProperty(ref _selectedCatalogCategory, value))
            {
                ApplyCatalogFilter();
            }
        }
    }

    private bool _isCatalogModalOpen;
    public bool IsCatalogModalOpen
    {
        get => _isCatalogModalOpen;
        set => SetProperty(ref _isCatalogModalOpen, value);
    }

    // Live Laptop Camera Barcode Scanner
    private bool _isCameraScannerOpen;
    public bool IsCameraScannerOpen
    {
        get => _isCameraScannerOpen;
        set => SetProperty(ref _isCameraScannerOpen, value);
    }

    private BitmapSource? _cameraFrameSource;
    public BitmapSource? CameraFrameSource
    {
        get => _cameraFrameSource;
        set
        {
            if (SetProperty(ref _cameraFrameSource, value))
            {
                OnPropertyChanged(nameof(HasCameraFrame));
                OnPropertyChanged(nameof(HasNoCameraFrame));
            }
        }
    }

    public bool HasCameraFrame => CameraFrameSource != null;
    public bool HasNoCameraFrame => CameraFrameSource == null;

    private string _cameraStatusText = "Initializing laptop webcam...";
    public string CameraStatusText
    {
        get => _cameraStatusText;
        set => SetProperty(ref _cameraStatusText, value);
    }

    private string _lastCameraScanResult = string.Empty;
    public string LastCameraScanResult
    {
        get => _lastCameraScanResult;
        set
        {
            if (SetProperty(ref _lastCameraScanResult, value))
            {
                OnPropertyChanged(nameof(HasLastScanResult));
            }
        }
    }

    public bool HasLastScanResult => !string.IsNullOrEmpty(LastCameraScanResult);

    private bool _cameraScanSuccess;
    public bool CameraScanSuccess
    {
        get => _cameraScanSuccess;
        set => SetProperty(ref _cameraScanSuccess, value);
    }

    private string _cameraScanFeedback = string.Empty;
    public string CameraScanFeedback
    {
        get => _cameraScanFeedback;
        set
        {
            if (SetProperty(ref _cameraScanFeedback, value))
            {
                OnPropertyChanged(nameof(HasCameraScanFeedback));
            }
        }
    }

    public bool HasCameraScanFeedback => !string.IsNullOrEmpty(CameraScanFeedback);

    private bool _autoCloseCameraAfterScan = true;
    public bool AutoCloseCameraAfterScan
    {
        get => _autoCloseCameraAfterScan;
        set => SetProperty(ref _autoCloseCameraAfterScan, value);
    }

    // Barcode input & quick search
    private string _barcodeQuery = string.Empty;
    public string BarcodeQuery
    {
        get => _barcodeQuery;
        set => SetProperty(ref _barcodeQuery, value);
    }

    // Customer
    private string _customerName = "Walk-in Customer";
    public string CustomerName
    {
        get => _customerName;
        set => SetProperty(ref _customerName, value);
    }

    private string _customerPhone = string.Empty;
    public string CustomerPhone
    {
        get => _customerPhone;
        set => SetProperty(ref _customerPhone, value);
    }

    // Totals
    private decimal _subTotal;
    public decimal SubTotal
    {
        get => _subTotal;
        set => SetProperty(ref _subTotal, value);
    }

    private decimal _billDiscountPercent;
    public decimal BillDiscountPercent
    {
        get => _billDiscountPercent;
        set
        {
            if (SetProperty(ref _billDiscountPercent, Math.Clamp(value, 0m, 100m)))
            {
                RecalculateTotals();
            }
        }
    }

    private decimal _billDiscountAmount;
    public decimal BillDiscountAmount
    {
        get => _billDiscountAmount;
        set => SetProperty(ref _billDiscountAmount, value);
    }

    private decimal _cgstAmount;
    public decimal CgstAmount
    {
        get => _cgstAmount;
        set => SetProperty(ref _cgstAmount, value);
    }

    private decimal _sgstAmount;
    public decimal SgstAmount
    {
        get => _sgstAmount;
        set => SetProperty(ref _sgstAmount, value);
    }

    private decimal _roundOff;
    public decimal RoundOff
    {
        get => _roundOff;
        set => SetProperty(ref _roundOff, value);
    }

    private decimal _grandTotal;
    public decimal GrandTotal
    {
        get => _grandTotal;
        set => SetProperty(ref _grandTotal, value);
    }

    // Payment
    private string _paymentMethod = "Cash";
    public string PaymentMethod
    {
        get => _paymentMethod;
        set
        {
            if (SetProperty(ref _paymentMethod, value))
            {
                OnPropertyChanged(nameof(IsCashPayment));
                OnPropertyChanged(nameof(IsUpiPayment));
                if (value == "UPI")
                {
                    GenerateUpiQrCode();
                }
            }
        }
    }

    public bool IsCashPayment => PaymentMethod == "Cash";
    public bool IsUpiPayment => PaymentMethod == "UPI";

    private decimal _cashReceived;
    public decimal CashReceived
    {
        get => _cashReceived;
        set
        {
            if (SetProperty(ref _cashReceived, value))
            {
                RecalculateChange();
            }
        }
    }

    private decimal _changeDue;
    public decimal ChangeDue
    {
        get => _changeDue;
        set => SetProperty(ref _changeDue, value);
    }

    // Static / Dynamic offline UPI QR Code image
    private BitmapSource? _upiQrCodeSource;
    public BitmapSource? UpiQrCodeSource
    {
        get => _upiQrCodeSource;
        set
        {
            if (SetProperty(ref _upiQrCodeSource, value))
            {
                OnPropertyChanged(nameof(HasUpiQrCode));
                OnPropertyChanged(nameof(HasUpiError));
            }
        }
    }

    public bool HasUpiQrCode => UpiQrCodeSource != null;
    public bool HasUpiError => !HasUpiQrCode;

    private string _upiErrorMessage = string.Empty;
    public string UpiErrorMessage
    {
        get => _upiErrorMessage;
        set => SetProperty(ref _upiErrorMessage, value);
    }

    public string UpiPayeeDisplay
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_storeSettings?.UpiVpa)) return string.Empty;
            var payee = string.IsNullOrWhiteSpace(_storeSettings.UpiPayeeName) ? _storeSettings.StoreName : _storeSettings.UpiPayeeName;
            return $"Paying To: {payee} ({_storeSettings.UpiVpa})";
        }
    }

    private bool _isUpiModalOpen;
    public bool IsUpiModalOpen
    {
        get => _isUpiModalOpen;
        set => SetProperty(ref _isUpiModalOpen, value);
    }

    // Receipt preview modal
    private bool _isReceiptPreviewOpen;
    public bool IsReceiptPreviewOpen
    {
        get => _isReceiptPreviewOpen;
        set => SetProperty(ref _isReceiptPreviewOpen, value);
    }

    private string _receiptPreviewText = string.Empty;
    public string ReceiptPreviewText
    {
        get => _receiptPreviewText;
        set => SetProperty(ref _receiptPreviewText, value);
    }

    private Invoice? _lastProcessedInvoice;
    public Invoice? LastProcessedInvoice
    {
        get => _lastProcessedInvoice;
        set => SetProperty(ref _lastProcessedInvoice, value);
    }

    public string CurrencySymbol => _storeSettings.CurrencySymbol;

    // Commands
    public IAsyncRelayCommand ProcessScanCommand { get; }
    public IRelayCommand NewBillCommand { get; }
    public IRelayCommand<CartItem> RemoveItemCommand { get; }
    public IRelayCommand<CartItem> IncrementQuantityCommand { get; }
    public IRelayCommand<CartItem> DecrementQuantityCommand { get; }
    public IAsyncRelayCommand CheckoutAndPrintCommand { get; }
    public IRelayCommand ShowReceiptPreviewCommand { get; }
    public IRelayCommand CloseReceiptPreviewCommand { get; }
    public IRelayCommand PrintReceiptPreviewCommand { get; }
    public IRelayCommand WindowsPrintCommand { get; }
    public IRelayCommand ToggleUpiModalCommand { get; }
    public IAsyncRelayCommand OpenCatalogModalCommand { get; }
    public IRelayCommand CloseCatalogModalCommand { get; }
    public IRelayCommand<CatalogPickerItem> AddPickerItemToCartCommand { get; }
    public IRelayCommand ToggleCameraScannerCommand { get; }
    public IRelayCommand CloseCameraScannerCommand { get; }

    public BillingViewModel(
        IBillingService billingService, 
        IPrinterService printerService, 
        IBarcodeService barcodeService,
        IInventoryService inventoryService,
        ICameraScannerService? cameraScannerService = null)
    {
        _billingService = billingService;
        _printerService = printerService;
        _barcodeService = barcodeService;
        _inventoryService = inventoryService;
        _cameraScannerService = cameraScannerService;

        if (_cameraScannerService != null)
        {
            _cameraScannerService.FrameCaptured += OnCameraFrameCaptured;
            _cameraScannerService.BarcodeDetected += OnCameraBarcodeDetected;
            _cameraScannerService.StatusChanged += OnCameraStatusChanged;
        }

        ProcessScanCommand = new AsyncRelayCommand(HandleBarcodeScanAsync);
        NewBillCommand = new RelayCommand(NewBill);
        RemoveItemCommand = new RelayCommand<CartItem>(RemoveItem);
        IncrementQuantityCommand = new RelayCommand<CartItem>(it => { if (it != null) it.Quantity++; });
        DecrementQuantityCommand = new RelayCommand<CartItem>(it => { if (it != null && it.Quantity > 1) it.Quantity--; });
        CheckoutAndPrintCommand = new AsyncRelayCommand(CheckoutAndPrintAsync);
        ShowReceiptPreviewCommand = new RelayCommand(ShowReceiptPreview);
        CloseReceiptPreviewCommand = new RelayCommand(CloseReceiptPreview);
        PrintReceiptPreviewCommand = new RelayCommand(PrintReceiptFromPreview);
        WindowsPrintCommand = new RelayCommand(WindowsPrint);
        ToggleUpiModalCommand = new AsyncRelayCommand(ToggleUpiModalAsync);
        OpenCatalogModalCommand = new AsyncRelayCommand(() => OpenCatalogModalAsync());
        CloseCatalogModalCommand = new RelayCommand(() => IsCatalogModalOpen = false);
        AddPickerItemToCartCommand = new RelayCommand<CatalogPickerItem>(AddPickerItemToCart);
        ToggleCameraScannerCommand = new RelayCommand(ToggleCameraScanner);
        CloseCameraScannerCommand = new RelayCommand(CloseCameraScanner);

        _ = LoadStoreSettingsAsync();
    }

    public void ToggleCameraScanner()
    {
        if (IsCameraScannerOpen)
        {
            CloseCameraScanner();
        }
        else
        {
            OpenCameraScanner();
        }
    }

    public void OpenCameraScanner()
    {
        if (_cameraScannerService == null)
        {
            StatusMessage = "Camera scanner service is not available.";
            return;
        }

        CameraFrameSource = null;
        LastCameraScanResult = string.Empty;
        CameraScanFeedback = string.Empty;
        CameraScanSuccess = false;
        CameraStatusText = "Connecting to laptop camera...";
        IsCameraScannerOpen = true;
        _cameraScannerService.Start();
    }

    public void CloseCameraScanner()
    {
        _cameraScannerService?.Stop();
        IsCameraScannerOpen = false;
        CameraFrameSource = null;
        CameraScanFeedback = string.Empty;
    }

    private void OnCameraFrameCaptured(object? sender, BitmapSource frame)
    {
        Application.Current?.Dispatcher?.BeginInvoke(() =>
        {
            if (IsCameraScannerOpen)
            {
                CameraFrameSource = frame;
            }
        });
    }

    private void OnCameraStatusChanged(object? sender, string status)
    {
        Application.Current?.Dispatcher?.BeginInvoke(() =>
        {
            CameraStatusText = status;
        });
    }

    private void OnCameraBarcodeDetected(object? sender, string barcode)
    {
        Application.Current?.Dispatcher?.BeginInvoke(async () =>
        {
            if (!IsCameraScannerOpen) return;

            LastCameraScanResult = barcode;

            bool added = await ProcessScannedBarcodeDirectAsync(barcode);

            if (added && AutoCloseCameraAfterScan)
            {
                await Task.Delay(900);
                if (IsCameraScannerOpen)
                {
                    CloseCameraScanner();
                }
            }
        });
    }

    public async Task<bool> ProcessScannedBarcodeDirectAsync(string scannedText)
    {
        if (string.IsNullOrWhiteSpace(scannedText)) return false;
        scannedText = scannedText.Trim();

        // 1. Direct match by Barcode or SKU
        var result = await _billingService.FindItemByBarcodeOrSkuAsync(scannedText);
        if (result != null)
        {
            AddItemToCart(result.Product, result.Variant);

            var variantInfo = result.Variant != null ? $" ({result.Variant.Size} - {result.Variant.Color})" : "";
            var price = (result.Variant != null && result.Variant.EffectivePrice > 0)
                ? result.Variant.EffectivePrice
                : result.Product.SellingPrice;

            CameraScanSuccess = true;
            CameraScanFeedback = $"✅ Added: {result.Product.Name}{variantInfo} — {_storeSettings.CurrencySymbol}{price:N2}";
            StatusMessage = $"Added {result.Product.Name}{variantInfo} to cart.";
            return true;
        }

        // 2. Search match fallback
        var searchResults = await _inventoryService.SearchProductsAsync(scannedText);
        if (searchResults.Count == 1)
        {
            var singleProduct = searchResults[0];
            if (singleProduct.Variants != null && singleProduct.Variants.Count == 1)
            {
                var v = singleProduct.Variants.First();
                AddItemToCart(singleProduct, v);
                var price = v.EffectivePrice > 0 ? v.EffectivePrice : singleProduct.SellingPrice;
                CameraScanSuccess = true;
                CameraScanFeedback = $"✅ Added: {singleProduct.Name} ({v.Size}) — {_storeSettings.CurrencySymbol}{price:N2}";
                StatusMessage = $"Added {singleProduct.Name} ({v.Size}) to cart.";
                return true;
            }
            else if (singleProduct.Variants == null || singleProduct.Variants.Count == 0)
            {
                AddItemToCart(singleProduct, null);
                CameraScanSuccess = true;
                CameraScanFeedback = $"✅ Added: {singleProduct.Name} — {_storeSettings.CurrencySymbol}{singleProduct.SellingPrice:N2}";
                StatusMessage = $"Added {singleProduct.Name} to cart.";
                return true;
            }
        }

        // 3. Not found in inventory
        CameraScanSuccess = false;
        CameraScanFeedback = $"⚠️ Barcode '{scannedText}' NOT found in inventory!";
        StatusMessage = $"[!] Barcode '{scannedText}' detected but not in inventory. Add it in Inventory first.";
        return false;
    }

    public async Task LoadStoreSettingsAsync()
    {
        _storeSettings = await _billingService.GetStoreSettingsAsync();
        OnPropertyChanged(nameof(CurrencySymbol));
        OnPropertyChanged(nameof(UpiPayeeDisplay));
        if (PaymentMethod == "UPI" || IsUpiModalOpen)
        {
            GenerateUpiQrCode();
        }
    }

    private async Task ToggleUpiModalAsync()
    {
        if (IsUpiModalOpen)
        {
            IsUpiModalOpen = false;
            return;
        }

        // 1. Fetch fresh store settings from DB in case edited in Settings
        await LoadStoreSettingsAsync();

        // 2. Select UPI payment mode
        PaymentMethod = "UPI";

        // 3. Generate the dynamic UPI QR code
        GenerateUpiQrCode();

        // 4. Open modal
        IsUpiModalOpen = true;
    }

    public async Task HandleBarcodeScanAsync()
    {
        if (string.IsNullOrWhiteSpace(BarcodeQuery)) return;

        var scannedText = BarcodeQuery.Trim();
        BarcodeQuery = string.Empty; // Reset buffer immediately for rapid scanning

        // 1. Check exact barcode or SKU scan lookup
        var result = await _billingService.FindItemByBarcodeOrSkuAsync(scannedText);
        if (result != null)
        {
            AddItemToCart(result.Product, result.Variant);
            return;
        }

        // 2. Check search by product name/SKU/category
        var searchResults = await _inventoryService.SearchProductsAsync(scannedText);
        if (searchResults.Count == 1)
        {
            var singleProduct = searchResults[0];
            if (singleProduct.Variants != null && singleProduct.Variants.Count == 1)
            {
                AddItemToCart(singleProduct, singleProduct.Variants.First());
                return;
            }
            else if (singleProduct.Variants == null || singleProduct.Variants.Count == 0)
            {
                AddItemToCart(singleProduct, null);
                return;
            }
        }

        // 3. If multiple results or manual text query, pop up Catalog Picker pre-filtered
        await OpenCatalogModalAsync(scannedText);
    }

    public void AddItemToCart(Product product, ProductVariant? variant)
    {
        var existing = CartItems.FirstOrDefault(c => 
            c.ProductId == product.Id && 
            (variant == null || c.VariantId == variant.Id));

        if (existing != null)
        {
            existing.Quantity++;
            StatusMessage = $"Incremented quantity for {existing.ItemName} ({existing.Size})";
        }
        else
        {
            var price = (variant != null && variant.EffectivePrice > 0)
                ? variant.EffectivePrice
                : product.SellingPrice;

            var cartItem = new CartItem
            {
                ProductId = product.Id,
                VariantId = variant?.Id,
                Sku = variant != null && !string.IsNullOrEmpty(variant.SkuVariant) ? variant.SkuVariant : product.Sku,
                Barcode = variant != null && !string.IsNullOrEmpty(variant.BarcodeVariant) ? variant.BarcodeVariant : product.Barcode,
                ItemName = product.Name,
                Size = variant?.Size ?? "Free Size",
                Color = variant?.Color ?? "Standard",
                UnitPrice = price,
                CostPrice = product.CostPrice,
                TaxRate = product.TaxRate,
                Quantity = 1
            };

            cartItem.Recalculate();
            cartItem.ItemChanged += RecalculateTotals;
            CartItems.Add(cartItem);

            StatusMessage = $"Added {cartItem.ItemName} ({cartItem.Size})";
        }

        RecalculateTotals();
    }

    public async Task OpenCatalogModalAsync(string? initialSearch = null)
    {
        IsBusy = true;
        try
        {
            var products = await _inventoryService.GetAllProductsAsync();
            _allCatalogItems.Clear();
            var catSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var p in products)
            {
                if (!string.IsNullOrWhiteSpace(p.Category))
                    catSet.Add(p.Category);

                if (p.Variants != null && p.Variants.Any())
                {
                    foreach (var v in p.Variants)
                    {
                        _allCatalogItems.Add(new CatalogPickerItem { Product = p, Variant = v });
                    }
                }
                else
                {
                    _allCatalogItems.Add(new CatalogPickerItem { Product = p, Variant = null });
                }
            }

            CatalogCategories.Clear();
            CatalogCategories.Add("All Categories");
            foreach (var c in catSet.OrderBy(x => x))
            {
                CatalogCategories.Add(c);
            }

            SelectedCatalogCategory = "All Categories";
            CatalogSearchQuery = initialSearch ?? string.Empty;
            ApplyCatalogFilter();
            IsCatalogModalOpen = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyCatalogFilter()
    {
        var q = CatalogSearchQuery?.Trim().ToLowerInvariant() ?? string.Empty;
        var cat = SelectedCatalogCategory;

        var filtered = _allCatalogItems.Where(item =>
        {
            if (cat != "All Categories" && !string.Equals(item.Category, cat, StringComparison.OrdinalIgnoreCase))
                return false;

            if (string.IsNullOrEmpty(q))
                return true;

            return item.Name.ToLowerInvariant().Contains(q) ||
                   item.Sku.ToLowerInvariant().Contains(q) ||
                   item.Barcode.ToLowerInvariant().Contains(q) ||
                   item.Size.ToLowerInvariant().Contains(q) ||
                   item.Color.ToLowerInvariant().Contains(q) ||
                   item.Category.ToLowerInvariant().Contains(q);
        });

        FilteredCatalogItems.Clear();
        foreach (var f in filtered)
        {
            FilteredCatalogItems.Add(f);
        }
    }

    public void AddPickerItemToCart(CatalogPickerItem? item)
    {
        if (item == null) return;
        AddItemToCart(item.Product, item.Variant);
    }

    private void RemoveItem(CartItem? item)
    {
        if (item != null && CartItems.Contains(item))
        {
            item.ItemChanged -= RecalculateTotals;
            CartItems.Remove(item);
            RecalculateTotals();
            StatusMessage = $"Removed {item.ItemName}";
        }
    }

    public void RecalculateTotals()
    {
        SubTotal = CartItems.Sum(c => c.UnitPrice * c.Quantity);
        var itemDiscounts = CartItems.Sum(c => c.DiscountAmount);

        // Bill-level discount
        BillDiscountAmount = (SubTotal - itemDiscounts) * (BillDiscountPercent / 100m);
        var totalDiscount = itemDiscounts + BillDiscountAmount;
        var taxableAmount = Math.Max(0m, SubTotal - totalDiscount);

        // 5% GST standard for apparel under ₹1000 or split equally as CGST + SGST (2.5% + 2.5%)
        // We calculate precise weighted GST
        decimal totalTax = 0;
        foreach (var item in CartItems)
        {
            var itemNet = item.TotalPrice * (1 - (BillDiscountPercent / 100m));
            totalTax += itemNet * (item.TaxRate / 100m);
        }

        CgstAmount = Math.Round(totalTax / 2m, 2);
        SgstAmount = Math.Round(totalTax / 2m, 2);

        var rawGrand = taxableAmount + totalTax;
        var roundedGrand = Math.Round(rawGrand, MidpointRounding.AwayFromZero);
        RoundOff = roundedGrand - rawGrand;
        GrandTotal = roundedGrand;

        if (CashReceived < GrandTotal)
        {
            CashReceived = GrandTotal;
        }
        RecalculateChange();

        if (PaymentMethod == "UPI")
        {
            GenerateUpiQrCode();
        }
    }

    private void RecalculateChange()
    {
        ChangeDue = Math.Max(0m, CashReceived - GrandTotal);
    }

    private void GenerateUpiQrCode()
    {
        if (string.IsNullOrWhiteSpace(_storeSettings?.UpiVpa))
        {
            UpiQrCodeSource = null;
            UpiErrorMessage = "⚠️ Merchant UPI ID not configured!\nGo to System Setup -> Offline UPI Configuration and enter your UPI ID (e.g. swappawar4912@okhdfcbank).";
            OnPropertyChanged(nameof(UpiErrorMessage));
            OnPropertyChanged(nameof(HasUpiError));
            OnPropertyChanged(nameof(HasUpiQrCode));
            return;
        }

        if (GrandTotal <= 0)
        {
            UpiQrCodeSource = null;
            UpiErrorMessage = "⚠️ Total is ₹0.00.\nScan garments or add items to the cart before generating UPI QR.";
            OnPropertyChanged(nameof(UpiErrorMessage));
            OnPropertyChanged(nameof(HasUpiError));
            OnPropertyChanged(nameof(HasUpiQrCode));
            return;
        }

        UpiErrorMessage = string.Empty;
        var invTemp = $"INV-{DateTime.Today:yyMMdd}-{DateTime.Now.Millisecond:D3}";
        var payee = string.IsNullOrWhiteSpace(_storeSettings.UpiPayeeName) ? _storeSettings.StoreName : _storeSettings.UpiPayeeName;
        var upiUri = $"upi://pay?pa={_storeSettings.UpiVpa.Trim()}&pn={Uri.EscapeDataString(payee.Trim())}&am={GrandTotal:0.00}&tn=ApparelBill_{invTemp}&cu=INR";
        UpiQrCodeSource = _barcodeService.GenerateQrCodeImageSource(upiUri, 260);
        OnPropertyChanged(nameof(UpiPayeeDisplay));
        OnPropertyChanged(nameof(UpiErrorMessage));
        OnPropertyChanged(nameof(HasUpiError));
        OnPropertyChanged(nameof(HasUpiQrCode));
    }

    public async Task CheckoutAndPrintAsync()
    {
        if (!CartItems.Any())
        {
            StatusMessage = "[!] Cart is empty. Scan items first.";
            return;
        }

        IsBusy = true;
        try
        {
            var invoice = new Invoice
            {
                CustomerName = string.IsNullOrWhiteSpace(CustomerName) ? "Walk-in Customer" : CustomerName.Trim(),
                CustomerPhone = CustomerPhone?.Trim() ?? string.Empty,
                PaymentMethod = PaymentMethod,
                SubTotal = SubTotal,
                DiscountPercent = BillDiscountPercent,
                DiscountAmount = BillDiscountAmount + CartItems.Sum(c => c.DiscountAmount),
                TaxAmount = CgstAmount + SgstAmount,
                CgstAmount = CgstAmount,
                SgstAmount = SgstAmount,
                RoundOff = RoundOff,
                TotalAmount = GrandTotal,
                CashReceived = PaymentMethod == "Cash" ? CashReceived : GrandTotal,
                ChangeDue = PaymentMethod == "Cash" ? ChangeDue : 0m,
                CashierName = "Admin Cashier",
                Items = CartItems.Select(c => new InvoiceItem
                {
                    ProductId = c.ProductId,
                    VariantId = c.VariantId,
                    Sku = c.Sku,
                    Barcode = c.Barcode,
                    ItemName = c.ItemName,
                    Size = c.Size,
                    Color = c.Color,
                    UnitPrice = c.UnitPrice,
                    CostPrice = c.CostPrice,
                    Quantity = c.Quantity,
                    DiscountPercent = c.DiscountPercent,
                    DiscountAmount = c.DiscountAmount,
                    TaxRate = c.TaxRate,
                    TaxAmount = c.TotalPrice * (c.TaxRate / 100m),
                    TotalPrice = c.TotalPrice
                }).ToList()
            };

            var processed = await _billingService.ProcessCheckoutAsync(invoice);
            LastProcessedInvoice = processed;

            // Check if thermal printer is configured or in digital PDF mode
            bool isDigitalOnly = string.IsNullOrWhiteSpace(_storeSettings.ThermalPrinterName) ||
                                 _storeSettings.ThermalPrinterName.StartsWith("None", StringComparison.OrdinalIgnoreCase) ||
                                 _storeSettings.ThermalPrinterName.Contains("PDF Preview", StringComparison.OrdinalIgnoreCase);

            if (isDigitalOnly)
            {
                StatusMessage = $"Invoice {processed.InvoiceNumber} saved! Showing digital receipt.";
                ShowReceiptPreview();
            }
            else
            {
                try
                {
                    _printerService.PrintReceipt(processed, _storeSettings);
                    StatusMessage = $"Invoice {processed.InvoiceNumber} checked out and printed successfully!";
                }
                catch (Exception printEx)
                {
                    StatusMessage = $"Invoice {processed.InvoiceNumber} saved! (Printer notice: {printEx.Message})";
                    ShowReceiptPreview();
                }
            }

            // Reset cart for next bill
            NewBill();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Checkout failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void ShowReceiptPreview()
    {
        if (LastProcessedInvoice == null && !CartItems.Any()) return;

        var inv = LastProcessedInvoice;
        if (inv == null)
        {
            // Transient invoice for preview before checkout
            inv = new Invoice
            {
                InvoiceNumber = "PREVIEW-TEMP",
                InvoiceDate = DateTime.Now,
                CustomerName = CustomerName,
                CustomerPhone = CustomerPhone,
                PaymentMethod = PaymentMethod,
                SubTotal = SubTotal,
                DiscountPercent = BillDiscountPercent,
                DiscountAmount = BillDiscountAmount,
                TaxAmount = CgstAmount + SgstAmount,
                CgstAmount = CgstAmount,
                SgstAmount = SgstAmount,
                RoundOff = RoundOff,
                TotalAmount = GrandTotal,
                CashReceived = CashReceived,
                ChangeDue = ChangeDue,
                CashierName = "Admin Cashier",
                Items = CartItems.Select(c => new InvoiceItem
                {
                    ItemName = c.ItemName,
                    Size = c.Size,
                    Color = c.Color,
                    Quantity = c.Quantity,
                    UnitPrice = c.UnitPrice,
                    TotalPrice = c.TotalPrice
                }).ToList()
            };
        }

        ReceiptPreviewText = _printerService.GenerateReceiptText(inv, _storeSettings);
        IsReceiptPreviewOpen = true;
    }

    public void CloseReceiptPreview()
    {
        IsReceiptPreviewOpen = false;
    }

    private void PrintReceiptFromPreview()
    {
        if (LastProcessedInvoice == null) return;
        try
        {
            _printerService.PrintReceipt(LastProcessedInvoice, _storeSettings);
            StatusMessage = "Receipt reprinted to thermal printer.";
            IsReceiptPreviewOpen = false;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Print error: {ex.Message}";
        }
    }

    private void WindowsPrint()
    {
        try
        {
            var printDialog = new System.Windows.Controls.PrintDialog();
            if (printDialog.ShowDialog() == true)
            {
                var flowDoc = new System.Windows.Documents.FlowDocument();
                flowDoc.PagePadding = new Thickness(24);
                var p = new System.Windows.Documents.Paragraph();
                p.FontFamily = new System.Windows.Media.FontFamily("Consolas");
                p.FontSize = 11;
                p.Inlines.Add(new System.Windows.Documents.Run(ReceiptPreviewText));
                flowDoc.Blocks.Add(p);

                var paginator = ((System.Windows.Documents.IDocumentPaginatorSource)flowDoc).DocumentPaginator;
                printDialog.PrintDocument(paginator, $"Invoice-{LastProcessedInvoice?.InvoiceNumber ?? "Preview"}");
                StatusMessage = "Receipt sent to Windows print spooler / PDF.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Print dialog notice: {ex.Message}";
        }
    }

    public void NewBill()
    {
        foreach (var item in CartItems)
        {
            item.ItemChanged -= RecalculateTotals;
        }
        CartItems.Clear();
        CustomerName = "Walk-in Customer";
        CustomerPhone = string.Empty;
        BillDiscountPercent = 0;
        PaymentMethod = "Cash";
        CashReceived = 0;
        ChangeDue = 0;
        BarcodeQuery = string.Empty;
        IsUpiModalOpen = false;
        RecalculateTotals();
        StatusMessage = "New bill started. Scan barcode or type SKU.";
    }
}
