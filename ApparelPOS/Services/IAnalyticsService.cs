using ApparelPOS.Models;

namespace ApparelPOS.Services;

public record SalesSummary(
    decimal TotalRevenue,
    int TotalInvoices,
    decimal TotalCost,
    decimal GrossProfit,
    decimal ProfitMarginPercent,
    decimal CashCollected,
    decimal CardCollected,
    decimal UpiCollected,
    int ItemsSold
);

public record SizeSalesStat(string Size, int QuantitySold, decimal Revenue);
public record CategorySalesStat(string Category, int QuantitySold, decimal Revenue);

public interface IAnalyticsService
{
    Task<SalesSummary> GetSalesSummaryAsync(DateTime fromDate, DateTime toDate);
    Task<List<SizeSalesStat>> GetTopSellingSizesAsync(DateTime fromDate, DateTime toDate, int top = 8);
    Task<List<CategorySalesStat>> GetTopSellingCategoriesAsync(DateTime fromDate, DateTime toDate);
    Task<List<Invoice>> GetInvoicesAsync(DateTime fromDate, DateTime toDate, string? search = null);
    Task<Invoice?> GetInvoiceByIdAsync(int id);
}
