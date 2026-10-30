using System.Collections.ObjectModel;
using ApparelPOS.Models;
using ApparelPOS.Services;
using CommunityToolkit.Mvvm.Input;

namespace ApparelPOS.ViewModels;

public class AnalyticsViewModel : ViewModelBase
{
    private readonly IAnalyticsService _analyticsService;
    private readonly IPrinterService _printerService;
    private readonly IBillingService _billingService;
    private StoreSettings _storeSettings = new();

    public ObservableCollection<Invoice> Invoices { get; } = new();
    public ObservableCollection<SizeSalesStat> TopSizes { get; } = new();
    public ObservableCollection<CategorySalesStat> TopCategories { get; } = new();

    private string _selectedPeriod = "Today";
    public string SelectedPeriod
    {
        get => _selectedPeriod;
        set
        {
            if (SetProperty(ref _selectedPeriod, value))
            {
                _ = LoadAnalyticsAsync();
            }
        }
    }

    private string _invoiceSearch = string.Empty;
    public string InvoiceSearch
    {
        get => _invoiceSearch;
        set
        {
            if (SetProperty(ref _invoiceSearch, value))
            {
                _ = LoadInvoicesAsync();
            }
        }
    }

    private Invoice? _selectedInvoice;
    public Invoice? SelectedInvoice
    {
        get => _selectedInvoice;
        set => SetProperty(ref _selectedInvoice, value);
    }

    // KPI Metrics
    private decimal _totalRevenue;
    public decimal TotalRevenue { get => _totalRevenue; set => SetProperty(ref _totalRevenue, value); }

    private int _totalInvoices;
    public int TotalInvoices { get => _totalInvoices; set => SetProperty(ref _totalInvoices, value); }

    private decimal _grossProfit;
    public decimal GrossProfit { get => _grossProfit; set => SetProperty(ref _grossProfit, value); }

    private decimal _profitMarginPercent;
    public decimal ProfitMarginPercent { get => _profitMarginPercent; set => SetProperty(ref _profitMarginPercent, value); }

    private int _itemsSold;
    public int ItemsSold { get => _itemsSold; set => SetProperty(ref _itemsSold, value); }

    private decimal _cashCollected;
    public decimal CashCollected { get => _cashCollected; set => SetProperty(ref _cashCollected, value); }

    private decimal _cardCollected;
    public decimal CardCollected { get => _cardCollected; set => SetProperty(ref _cardCollected, value); }

    private decimal _upiCollected;
    public decimal UpiCollected { get => _upiCollected; set => SetProperty(ref _upiCollected, value); }

    public string CurrencySymbol => _storeSettings.CurrencySymbol;

    // Receipt Preview Modal for Reprint
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

    private Invoice? _selectedReceiptInvoice;
    public Invoice? SelectedReceiptInvoice
    {
        get => _selectedReceiptInvoice;
        set => SetProperty(ref _selectedReceiptInvoice, value);
    }

    // Commands
    public IAsyncRelayCommand RefreshCommand { get; }
    public IAsyncRelayCommand<Invoice> ReprintReceiptCommand { get; }
    public IRelayCommand CloseReceiptPreviewCommand { get; }
    public IRelayCommand WindowsPrintCommand { get; }
    public IRelayCommand PrintReceiptPreviewCommand { get; }

    public AnalyticsViewModel(
        IAnalyticsService analyticsService,
        IPrinterService printerService,
        IBillingService billingService)
    {
        _analyticsService = analyticsService;
        _printerService = printerService;
        _billingService = billingService;

        RefreshCommand = new AsyncRelayCommand(LoadAnalyticsAsync);
        ReprintReceiptCommand = new AsyncRelayCommand<Invoice>(ReprintReceiptAsync);
        CloseReceiptPreviewCommand = new RelayCommand(CloseReceiptPreview);
        WindowsPrintCommand = new RelayCommand(WindowsPrint);
        PrintReceiptPreviewCommand = new RelayCommand(PrintReceiptFromPreview);

        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        _storeSettings = await _billingService.GetStoreSettingsAsync();
        OnPropertyChanged(nameof(CurrencySymbol));
        await LoadAnalyticsAsync();
    }

    private (DateTime from, DateTime to) GetDateRange()
    {
        var now = DateTime.Today;
        return SelectedPeriod switch
        {
            "Yesterday" => (now.AddDays(-1), now.AddDays(-1)),
            "Last 7 Days" => (now.AddDays(-7), now),
            "This Month" => (new DateTime(now.Year, now.Month, 1), now),
            "All Time" => (now.AddYears(-5), now),
            _ => (now, now) // "Today"
        };
    }

    public async Task LoadAnalyticsAsync()
    {
        IsBusy = true;
        try
        {
            var (from, to) = GetDateRange();

            var summary = await _analyticsService.GetSalesSummaryAsync(from, to);
            TotalRevenue = summary.TotalRevenue;
            TotalInvoices = summary.TotalInvoices;
            GrossProfit = summary.GrossProfit;
            ProfitMarginPercent = summary.ProfitMarginPercent;
            ItemsSold = summary.ItemsSold;
            CashCollected = summary.CashCollected;
            CardCollected = summary.CardCollected;
            UpiCollected = summary.UpiCollected;

            // Load top sizes
            var sizes = await _analyticsService.GetTopSellingSizesAsync(from, to);
            TopSizes.Clear();
            foreach (var s in sizes)
            {
                TopSizes.Add(s);
            }

            // Load top categories
            var cats = await _analyticsService.GetTopSellingCategoriesAsync(from, to);
            TopCategories.Clear();
            foreach (var c in cats)
            {
                TopCategories.Add(c);
            }

            await LoadInvoicesAsync();
            StatusMessage = $"Analytics refreshed for {SelectedPeriod}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Analytics error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadInvoicesAsync()
    {
        var (from, to) = GetDateRange();
        var list = await _analyticsService.GetInvoicesAsync(from, to, InvoiceSearch);
        Invoices.Clear();
        foreach (var inv in list)
        {
            Invoices.Add(inv);
        }
    }

    private async Task ReprintReceiptAsync(Invoice? invoice)
    {
        var target = invoice ?? SelectedInvoice;
        if (target == null) return;

        try
        {
            var full = await _analyticsService.GetInvoiceByIdAsync(target.Id);
            if (full != null)
            {
                SelectedReceiptInvoice = full;
                ReceiptPreviewText = _printerService.GenerateReceiptText(full, _storeSettings);
                IsReceiptPreviewOpen = true;
                StatusMessage = $"Viewing receipt for {full.InvoiceNumber}.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Reprint failed: {ex.Message}";
        }
    }

    public void CloseReceiptPreview()
    {
        IsReceiptPreviewOpen = false;
    }

    private void WindowsPrint()
    {
        try
        {
            var printDialog = new System.Windows.Controls.PrintDialog();
            if (printDialog.ShowDialog() == true)
            {
                var flowDoc = new System.Windows.Documents.FlowDocument();
                flowDoc.PagePadding = new System.Windows.Thickness(24);
                var p = new System.Windows.Documents.Paragraph();
                p.FontFamily = new System.Windows.Media.FontFamily("Consolas");
                p.FontSize = 11;
                p.Inlines.Add(new System.Windows.Documents.Run(ReceiptPreviewText));
                flowDoc.Blocks.Add(p);

                var paginator = ((System.Windows.Documents.IDocumentPaginatorSource)flowDoc).DocumentPaginator;
                printDialog.PrintDocument(paginator, $"Invoice-{SelectedReceiptInvoice?.InvoiceNumber ?? "Reprint"}");
                StatusMessage = "Receipt sent to Windows print spooler / PDF.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Print dialog notice: {ex.Message}";
        }
    }

    private void PrintReceiptFromPreview()
    {
        if (SelectedReceiptInvoice == null) return;
        try
        {
            var targetPrinter = _storeSettings.ThermalPrinterName;
            if (string.IsNullOrWhiteSpace(targetPrinter) || 
                targetPrinter.StartsWith("None", StringComparison.OrdinalIgnoreCase) || 
                targetPrinter.Contains("PDF Preview", StringComparison.OrdinalIgnoreCase))
            {
                WindowsPrint();
                return;
            }

            _printerService.PrintReceipt(SelectedReceiptInvoice, _storeSettings);
            StatusMessage = $"Receipt for {SelectedReceiptInvoice.InvoiceNumber} sent to thermal printer.";
            IsReceiptPreviewOpen = false;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Thermal print error: {ex.Message}. Opening print dialog...";
            WindowsPrint();
        }
    }
}
