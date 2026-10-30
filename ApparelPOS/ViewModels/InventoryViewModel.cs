using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using ApparelPOS.Models;
using ApparelPOS.Services;
using CommunityToolkit.Mvvm.Input;

namespace ApparelPOS.ViewModels;

public class InventoryViewModel : ViewModelBase
{
    private readonly IInventoryService _inventoryService;

    public ObservableCollection<Product> Products { get; } = new();
    public ObservableCollection<string> Categories { get; } = new();
    public ObservableCollection<string> CategorySuggestions { get; } = new();

    private string _searchQuery = string.Empty;
    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value))
            {
                _ = LoadProductsAsync();
            }
        }
    }

    private string _selectedCategory = "All Categories";
    public string SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (SetProperty(ref _selectedCategory, value))
            {
                _ = LoadProductsAsync();
            }
        }
    }

    private bool _lowStockOnly;
    public bool LowStockOnly
    {
        get => _lowStockOnly;
        set
        {
            if (SetProperty(ref _lowStockOnly, value))
            {
                _ = LoadProductsAsync();
            }
        }
    }

    private Product? _selectedProduct;
    public Product? SelectedProduct
    {
        get => _selectedProduct;
        set
        {
            if (SetProperty(ref _selectedProduct, value))
            {
                OnPropertyChanged(nameof(HasSelectedProduct));
                OnPropertyChanged(nameof(HasVariants));
                OnPropertyChanged(nameof(HasNoVariants));
            }
        }
    }

    public bool HasSelectedProduct => SelectedProduct != null;
    public bool HasVariants => SelectedProduct != null && SelectedProduct.Variants != null && SelectedProduct.Variants.Count > 0;
    public bool HasNoVariants => SelectedProduct != null && (SelectedProduct.Variants == null || SelectedProduct.Variants.Count == 0);

    private bool _isNewProduct;
    public bool IsNewProduct
    {
        get => _isNewProduct;
        set => SetProperty(ref _isNewProduct, value);
    }

    // Modal state for Add/Edit Product
    private bool _isEditProductModalOpen;
    public bool IsEditProductModalOpen
    {
        get => _isEditProductModalOpen;
        set => SetProperty(ref _isEditProductModalOpen, value);
    }

    // Modal state for Add Variant
    private bool _isAddVariantModalOpen;
    public bool IsAddVariantModalOpen
    {
        get => _isAddVariantModalOpen;
        set => SetProperty(ref _isAddVariantModalOpen, value);
    }

    // Modal state for Quick Add Stock / Stock Adjustment
    private bool _isAddStockModalOpen;
    public bool IsAddStockModalOpen
    {
        get => _isAddStockModalOpen;
        set => SetProperty(ref _isAddStockModalOpen, value);
    }

    private ProductVariant? _stockTargetVariant;
    public ProductVariant? StockTargetVariant
    {
        get => _stockTargetVariant;
        set
        {
            if (SetProperty(ref _stockTargetVariant, value))
            {
                OnPropertyChanged(nameof(ExpectedNewStock));
            }
        }
    }

    private string _stockAdjustmentMode = "Add"; // "Add", "Deduct", "Set"
    public string StockAdjustmentMode
    {
        get => _stockAdjustmentMode;
        set
        {
            if (SetProperty(ref _stockAdjustmentMode, value))
            {
                OnPropertyChanged(nameof(ExpectedNewStock));
                OnPropertyChanged(nameof(IsAddMode));
                OnPropertyChanged(nameof(IsDeductMode));
                OnPropertyChanged(nameof(IsSetMode));
                OnPropertyChanged(nameof(ModalTitle));
                OnPropertyChanged(nameof(InputQuantityLabel));
                OnPropertyChanged(nameof(ModalActionButtonText));
            }
        }
    }

    public bool IsAddMode => StockAdjustmentMode == "Add";
    public bool IsDeductMode => StockAdjustmentMode == "Deduct";
    public bool IsSetMode => StockAdjustmentMode == "Set";

    public string ModalTitle => StockAdjustmentMode switch
    {
        "Deduct" => "➖ DEDUCT / REDUCE STOCK (STOCK-OUT)",
        "Set" => "🎯 SET EXACT STOCK QUANTITY (CLEAR OR RE-COUNT)",
        _ => "➕ RECEIVE & ADD STOCK (STOCK-IN)"
    };

    public string InputQuantityLabel => StockAdjustmentMode switch
    {
        "Deduct" => "QUANTITY TO DEDUCT (DAMAGE / RETURN / CORRECTION) *",
        "Set" => "NEW EXACT STOCK QUANTITY (ENTER 0 TO CLEAR) *",
        _ => "QUANTITY OF UNITS TO ADD (RECEIVE) *"
    };

    public string ModalActionButtonText => StockAdjustmentMode switch
    {
        "Deduct" => "✓ Deduct Stock Units",
        "Set" => "✓ Set Exact Stock Quantity",
        _ => "✓ Add Stock Units"
    };

    private int _stockToAdd = 10;
    public int StockToAdd
    {
        get => _stockToAdd;
        set
        {
            if (SetProperty(ref _stockToAdd, Math.Max(0, value)))
            {
                OnPropertyChanged(nameof(ExpectedNewStock));
            }
        }
    }

    public int ExpectedNewStock
    {
        get
        {
            int current = StockTargetVariant?.StockQuantity ?? 0;
            return StockAdjustmentMode switch
            {
                "Deduct" => Math.Max(0, current - StockToAdd),
                "Set" => Math.Max(0, StockToAdd),
                _ => current + StockToAdd
            };
        }
    }

    private int _newProductInitialStock = 20;
    public int NewProductInitialStock
    {
        get => _newProductInitialStock;
        set => SetProperty(ref _newProductInitialStock, Math.Max(0, value));
    }

    // Form fields for editing/adding product
    private Product _editingProduct = new();
    public Product EditingProduct
    {
        get => _editingProduct;
        set => SetProperty(ref _editingProduct, value);
    }

    // Form fields for adding variant
    private ProductVariant _newVariant = new();
    public ProductVariant NewVariant
    {
        get => _newVariant;
        set => SetProperty(ref _newVariant, value);
    }

    // Category Management Modal
    private bool _isAddCategoryModalOpen;
    public bool IsAddCategoryModalOpen
    {
        get => _isAddCategoryModalOpen;
        set => SetProperty(ref _isAddCategoryModalOpen, value);
    }

    private string _newCategoryName = string.Empty;
    public string NewCategoryName
    {
        get => _newCategoryName;
        set => SetProperty(ref _newCategoryName, value);
    }

    private string _newCategoryHsn = "6205";
    public string NewCategoryHsn
    {
        get => _newCategoryHsn;
        set => SetProperty(ref _newCategoryHsn, value);
    }

    private decimal _newCategoryTaxRate = 5.0m;
    public decimal NewCategoryTaxRate
    {
        get => _newCategoryTaxRate;
        set => SetProperty(ref _newCategoryTaxRate, value);
    }

    // Commands
    public IAsyncRelayCommand RefreshCommand { get; }
    public IRelayCommand OpenAddCategoryModalCommand { get; }
    public IRelayCommand CloseAddCategoryModalCommand { get; }
    public IAsyncRelayCommand SaveCategoryCommand { get; }
    public IRelayCommand<string> QuickSelectSuggestionCommand { get; }
    public IRelayCommand OpenAddProductModalCommand { get; }
    public IRelayCommand<Product> OpenEditProductModalCommand { get; }
    public IAsyncRelayCommand SaveProductCommand { get; }
    public IAsyncRelayCommand<Product> DeleteProductCommand { get; }
    public IRelayCommand OpenAddVariantModalCommand { get; }
    public IAsyncRelayCommand SaveVariantCommand { get; }
    public IAsyncRelayCommand<ProductVariant> DeleteVariantCommand { get; }
    public IAsyncRelayCommand ExportCsvCommand { get; }
    public IAsyncRelayCommand ImportCsvCommand { get; }

    // Direct Stock Adjustment Commands
    public IRelayCommand<ProductVariant> OpenAddStockModalCommand { get; }
    public IAsyncRelayCommand ConfirmAddStockCommand { get; }
    public IAsyncRelayCommand<ProductVariant> QuickIncrementStockCommand { get; }
    public IAsyncRelayCommand<ProductVariant> QuickDecrementStockCommand { get; }
    public IAsyncRelayCommand<ProductVariant> QuickClearVariantStockCommand { get; }
    public IAsyncRelayCommand<Product> ClearSelectedProductStockCommand { get; }
    public IRelayCommand<string> SetStockAdjustmentModeCommand { get; }
    public IRelayCommand SetStockToZeroCommand { get; }
    public IRelayCommand<object> SetStockToAddPresetCommand { get; }
    public IAsyncRelayCommand<Product> QuickGenerateStandardSizesCommand { get; }
    public IRelayCommand<Product> SelectProductCommand { get; }

    public InventoryViewModel(IInventoryService inventoryService)
    {
        _inventoryService = inventoryService;

        RefreshCommand = new AsyncRelayCommand(LoadProductsAsync);
        OpenAddCategoryModalCommand = new RelayCommand(OpenAddCategoryModal);
        CloseAddCategoryModalCommand = new RelayCommand(CloseAddCategoryModal);
        SaveCategoryCommand = new AsyncRelayCommand(SaveCategoryAsync);
        QuickSelectSuggestionCommand = new RelayCommand<string>(sug =>
        {
            if (!string.IsNullOrEmpty(sug))
            {
                NewCategoryName = sug;
            }
        });
        OpenAddProductModalCommand = new RelayCommand(OpenAddProductModal);
        OpenEditProductModalCommand = new RelayCommand<Product>(OpenEditProductModal);
        SaveProductCommand = new AsyncRelayCommand(SaveProductAsync);
        DeleteProductCommand = new AsyncRelayCommand<Product>(DeleteProductAsync);
        OpenAddVariantModalCommand = new RelayCommand(OpenAddVariantModal);
        SaveVariantCommand = new AsyncRelayCommand(SaveVariantAsync);
        DeleteVariantCommand = new AsyncRelayCommand<ProductVariant>(DeleteVariantAsync);
        ExportCsvCommand = new AsyncRelayCommand(ExportCsvAsync);
        ImportCsvCommand = new AsyncRelayCommand(ImportCsvAsync);

        OpenAddStockModalCommand = new RelayCommand<ProductVariant>(OpenAddStockModal);
        ConfirmAddStockCommand = new AsyncRelayCommand(ConfirmAddStockAsync);
        QuickIncrementStockCommand = new AsyncRelayCommand<ProductVariant>(QuickIncrementStockAsync);
        QuickDecrementStockCommand = new AsyncRelayCommand<ProductVariant>(QuickDecrementStockAsync);
        QuickClearVariantStockCommand = new AsyncRelayCommand<ProductVariant>(QuickClearVariantStockAsync);
        ClearSelectedProductStockCommand = new AsyncRelayCommand<Product>(ClearSelectedProductStockAsync);
        SetStockAdjustmentModeCommand = new RelayCommand<string>(mode =>
        {
            if (!string.IsNullOrEmpty(mode))
            {
                StockAdjustmentMode = mode;
            }
        });
        SetStockToZeroCommand = new RelayCommand(() =>
        {
            StockAdjustmentMode = "Set";
            StockToAdd = 0;
        });
        SetStockToAddPresetCommand = new RelayCommand<object>(param =>
        {
            if (param != null && int.TryParse(param.ToString(), out var val))
            {
                StockToAdd = val;
            }
        });
        QuickGenerateStandardSizesCommand = new AsyncRelayCommand<Product>(QuickGenerateSizesAsync);
        SelectProductCommand = new RelayCommand<Product>(p => { if (p != null) SelectedProduct = p; });

        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        await LoadCategoriesAsync();
        await LoadProductsAsync();
    }

    private async Task LoadCategoriesAsync()
    {
        var cats = await _inventoryService.GetCategoriesAsync();
        var selectedCat = SelectedCategory;
        Categories.Clear();
        Categories.Add("All Categories");
        CategorySuggestions.Clear();

        foreach (var c in cats)
        {
            Categories.Add(c.Name);
            CategorySuggestions.Add(c.Name);
        }

        var commonSuggestions = new[] { "Shirts", "Casual Shirts", "Formal Shirts", "T-Shirts", "Jeans", "Trousers", "Kurtas", "Dresses", "Jackets", "Accessories" };
        foreach (var sug in commonSuggestions)
        {
            if (!CategorySuggestions.Any(c => string.Equals(c, sug, StringComparison.OrdinalIgnoreCase)))
            {
                CategorySuggestions.Add(sug);
            }
        }

        if (Categories.Contains(selectedCat))
        {
            SelectedCategory = selectedCat;
        }
    }

    public async Task LoadProductsAsync()
    {
        IsBusy = true;
        try
        {
            var currentId = SelectedProduct?.Id;
            var list = await _inventoryService.SearchProductsAsync(SearchQuery, SelectedCategory, LowStockOnly);
            Products.Clear();
            foreach (var item in list)
            {
                Products.Add(item);
            }

            if (currentId.HasValue)
            {
                SelectedProduct = Products.FirstOrDefault(p => p.Id == currentId.Value) ?? Products.FirstOrDefault();
            }
            else
            {
                SelectedProduct = Products.FirstOrDefault();
            }

            OnPropertyChanged(nameof(HasVariants));
            OnPropertyChanged(nameof(HasNoVariants));
            StatusMessage = $"Loaded {Products.Count} products.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading products: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void OpenAddCategoryModal()
    {
        NewCategoryName = string.Empty;
        NewCategoryHsn = "6205";
        NewCategoryTaxRate = 5.0m;
        IsAddCategoryModalOpen = true;
    }

    public void CloseAddCategoryModal()
    {
        IsAddCategoryModalOpen = false;
    }

    public async Task SaveCategoryAsync()
    {
        if (string.IsNullOrWhiteSpace(NewCategoryName))
        {
            StatusMessage = "Please enter a valid category name.";
            return;
        }

        var catName = NewCategoryName.Trim();
        try
        {
            var hsn = string.IsNullOrWhiteSpace(NewCategoryHsn) ? "6205" : NewCategoryHsn.Trim();
            var tax = NewCategoryTaxRate >= 0 ? NewCategoryTaxRate : 5.0m;

            await _inventoryService.AddCategoryAsync(catName, hsn, tax);
            await LoadCategoriesAsync();

            if (IsEditProductModalOpen && EditingProduct != null)
            {
                EditingProduct.Category = catName;
                if (EditingProduct.TaxRate == 0)
                {
                    EditingProduct.TaxRate = tax;
                }
                OnPropertyChanged(nameof(EditingProduct));
            }

            IsAddCategoryModalOpen = false;
            StatusMessage = $"Category '{catName}' added successfully!";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error adding category: {ex.Message}";
        }
    }

    private void OpenAddProductModal()
    {
        IsNewProduct = true;
        NewProductInitialStock = 20;
        EditingProduct = new Product
        {
            Sku = $"SKU-{DateTime.Now:yyMMddHHmm}",
            Barcode = $"{DateTime.Now:yyMMddHHmmss}",
            Category = Categories.Count > 1 ? Categories[1] : "Shirts",
            SellingPrice = 999.00m,
            CostPrice = 500.00m,
            TaxRate = 5.0m,
            LowStockThreshold = 5
        };
        IsEditProductModalOpen = true;
    }

    private void OpenEditProductModal(Product? product)
    {
        var target = product ?? SelectedProduct;
        if (target == null) return;
        SelectedProduct = target;
        IsNewProduct = false;
        EditingProduct = new Product
        {
            Id = target.Id,
            Sku = target.Sku,
            Barcode = target.Barcode,
            Name = target.Name,
            Category = target.Category,
            Description = target.Description,
            CostPrice = target.CostPrice,
            SellingPrice = target.SellingPrice,
            TaxRate = target.TaxRate,
            LowStockThreshold = target.LowStockThreshold,
            IsActive = target.IsActive,
            CreatedAt = target.CreatedAt
        };
        IsEditProductModalOpen = true;
    }

    private async Task SaveProductAsync()
    {
        if (string.IsNullOrWhiteSpace(EditingProduct.Name) || string.IsNullOrWhiteSpace(EditingProduct.Sku))
        {
            StatusMessage = "Product Name and SKU are required.";
            return;
        }

        try
        {
            bool isNew = EditingProduct.Id == 0;
            var saved = await _inventoryService.SaveProductAsync(EditingProduct);

            // Automatically create initial variant with stock if new product
            if (isNew && NewProductInitialStock > 0)
            {
                var defaultVariant = new ProductVariant
                {
                    ProductId = saved.Id,
                    Size = "Free Size (M)",
                    Color = "Standard",
                    Fabric = "100% Cotton",
                    FitType = "Regular Fit",
                    SkuVariant = $"{saved.Sku}-01",
                    BarcodeVariant = saved.Barcode,
                    StockQuantity = NewProductInitialStock
                };
                await _inventoryService.SaveVariantAsync(defaultVariant);
            }

            IsEditProductModalOpen = false;
            await LoadCategoriesAsync();
            await LoadProductsAsync();
            StatusMessage = $"Product '{saved.Name}' saved successfully with initial stock.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to save product: {ex.Message}";
        }
    }

    public async Task QuickGenerateSizesAsync(Product? product)
    {
        var target = product ?? SelectedProduct;
        if (target == null) return;

        try
        {
            var sizes = new[] { ("S", 10), ("M", 15), ("L", 15), ("XL", 10) };
            int idx = (target.Variants?.Count ?? 0) + 1;
            foreach (var (size, stock) in sizes)
            {
                var v = new ProductVariant
                {
                    ProductId = target.Id,
                    Size = size,
                    Color = "Standard",
                    Fabric = "100% Cotton",
                    FitType = "Regular Fit",
                    SkuVariant = $"{target.Sku}-{size}",
                    BarcodeVariant = $"{target.Barcode}{idx:D2}",
                    StockQuantity = stock
                };
                await _inventoryService.SaveVariantAsync(v);
                idx++;
            }
            await LoadProductsAsync();
            StatusMessage = $"Added sizes S, M, L, XL with 50 units total stock to {target.Name}!";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to generate sizes: {ex.Message}";
        }
    }

    private async Task DeleteProductAsync(Product? product)
    {
        var target = product ?? SelectedProduct;
        if (target == null) return;

        var confirm = MessageBox.Show(
            $"Are you sure you want to permanently delete product '{target.Name}' (SKU: {target.Sku}) and all its sizes/stock from the catalog?",
            "Confirm Delete Product",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm == MessageBoxResult.Yes)
        {
            await _inventoryService.DeleteProductAsync(target.Id);
            if (SelectedProduct?.Id == target.Id)
            {
                SelectedProduct = null;
            }
            await LoadProductsAsync();
            StatusMessage = $"Product '{target.Name}' and all variants deleted.";
        }
    }

    private void OpenAddVariantModal()
    {
        if (SelectedProduct == null) return;

        var suffix = (SelectedProduct.Variants?.Count ?? 0) + 1;
        NewVariant = new ProductVariant
        {
            ProductId = SelectedProduct.Id,
            Size = "M",
            Color = "Navy Blue",
            Fabric = "100% Cotton",
            FitType = "Slim Fit",
            SkuVariant = $"{SelectedProduct.Sku}-V{suffix}",
            BarcodeVariant = $"{SelectedProduct.Barcode}{suffix:D2}",
            StockQuantity = 10
        };
        IsAddVariantModalOpen = true;
    }

    private async Task SaveVariantAsync()
    {
        if (SelectedProduct == null) return;

        try
        {
            NewVariant.ProductId = SelectedProduct.Id;
            await _inventoryService.SaveVariantAsync(NewVariant);
            IsAddVariantModalOpen = false;
            await LoadProductsAsync();
            StatusMessage = $"Variant {NewVariant.Size}/{NewVariant.Color} added.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to save variant: {ex.Message}";
        }
    }

    private async Task DeleteVariantAsync(ProductVariant? variant)
    {
        if (variant == null) return;

        var confirm = MessageBox.Show($"Permanently delete variant {variant.Size} - {variant.Color} and all its stock?", "Confirm Delete Size Variant", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm == MessageBoxResult.Yes)
        {
            await _inventoryService.DeleteVariantAsync(variant.Id);
            await LoadProductsAsync();
            StatusMessage = $"Size variant {variant.Size} deleted.";
        }
    }

    private void OpenAddStockModal(ProductVariant? variant)
    {
        if (variant == null) return;
        StockTargetVariant = variant;
        StockAdjustmentMode = "Add";
        StockToAdd = 10;
        IsAddStockModalOpen = true;
    }

    private async Task ConfirmAddStockAsync()
    {
        if (StockTargetVariant == null) return;

        try
        {
            int previous = StockTargetVariant.StockQuantity;
            int newQty = StockAdjustmentMode switch
            {
                "Deduct" => Math.Max(0, previous - StockToAdd),
                "Set" => Math.Max(0, StockToAdd),
                _ => previous + StockToAdd
            };

            StockTargetVariant.StockQuantity = newQty;
            await _inventoryService.SaveVariantAsync(StockTargetVariant);
            IsAddStockModalOpen = false;
            await LoadProductsAsync();
            StatusMessage = $"Updated stock for {StockTargetVariant.Size} - {StockTargetVariant.Color}: {previous} -> {newQty} units.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to update stock: {ex.Message}";
        }
    }

    private async Task QuickClearVariantStockAsync(ProductVariant? variant)
    {
        if (variant == null) return;

        var confirm = MessageBox.Show(
            $"Clear stock for size '{variant.Size}' ({variant.Color}) from {variant.StockQuantity} to 0 units?",
            "Clear Stock to Zero",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm == MessageBoxResult.Yes)
        {
            variant.StockQuantity = 0;
            await _inventoryService.SaveVariantAsync(variant);
            await LoadProductsAsync();
            StatusMessage = $"Stock for size {variant.Size} cleared to 0 units.";
        }
    }

    private async Task ClearSelectedProductStockAsync(Product? product)
    {
        var target = product ?? SelectedProduct;
        if (target == null || target.Variants == null || !target.Variants.Any())
        {
            MessageBox.Show("No size variants found for this product.", "No Variants", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var confirm = MessageBox.Show(
            $"Reset ALL stock quantities for '{target.Name}' to 0 units?\n\n(This clears inventory count to 0 across all {target.Variants.Count} sizes while keeping the garment in your catalog).",
            "Reset All Product Stock to 0",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm == MessageBoxResult.Yes)
        {
            foreach (var v in target.Variants)
            {
                v.StockQuantity = 0;
                await _inventoryService.SaveVariantAsync(v);
            }
            await LoadProductsAsync();
            StatusMessage = $"All stock for '{target.Name}' cleared to 0 units.";
        }
    }

    private async Task QuickIncrementStockAsync(ProductVariant? variant)
    {
        if (variant == null) return;
        try
        {
            variant.StockQuantity += 1;
            await _inventoryService.SaveVariantAsync(variant);
            await LoadProductsAsync();
            StatusMessage = $"Stock incremented for {variant.Size} - {variant.Color} (Total: {variant.StockQuantity})";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    private async Task QuickDecrementStockAsync(ProductVariant? variant)
    {
        if (variant == null || variant.StockQuantity <= 0) return;
        try
        {
            variant.StockQuantity -= 1;
            await _inventoryService.SaveVariantAsync(variant);
            await LoadProductsAsync();
            StatusMessage = $"Stock decremented for {variant.Size} - {variant.Color} (Total: {variant.StockQuantity})";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }


    private async Task ExportCsvAsync()
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "CSV Files (*.csv)|*.csv",
            FileName = $"ApparelInventory_{DateTime.Now:yyyyMMdd}.csv",
            Title = "Export Inventory to CSV"
        };

        if (dlg.ShowDialog() == true)
        {
            try
            {
                var count = await _inventoryService.ExportToCsvAsync(dlg.FileName);
                StatusMessage = $"Exported {count} items/variants to CSV file successfully.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Export failed: {ex.Message}";
            }
        }
    }

    private async Task ImportCsvAsync()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
            Title = "Import Inventory CSV"
        };

        if (dlg.ShowDialog() == true)
        {
            try
            {
                var (imported, updated, errors) = await _inventoryService.ImportFromCsvAsync(dlg.FileName);
                await LoadProductsAsync();
                
                var msg = $"CSV Import Complete: {imported} new items imported, {updated} updated.";
                if (errors.Any())
                {
                    msg += $" ({errors.Count} warnings/errors)";
                }
                StatusMessage = msg;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Import error: {ex.Message}";
            }
        }
    }
}
