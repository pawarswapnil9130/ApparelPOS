using ApparelPOS.Data;
using ApparelPOS.Models;
using Microsoft.EntityFrameworkCore;

namespace ApparelPOS.Services;

public class AnalyticsService : IAnalyticsService
{
    private readonly AppDbContext _context;

    public AnalyticsService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<SalesSummary> GetSalesSummaryAsync(DateTime fromDate, DateTime toDate)
    {
        var start = fromDate.Date;
        var end = toDate.Date.AddDays(1).AddTicks(-1);

        var invoices = await _context.Invoices
            .Include(i => i.Items)
            .Where(i => i.InvoiceDate >= start && i.InvoiceDate <= end)
            .ToListAsync();

        if (!invoices.Any())
        {
            return new SalesSummary(0, 0, 0, 0, 0, 0, 0, 0, 0);
        }

        decimal totalRevenue = invoices.Sum(i => i.TotalAmount);
        int totalInvoices = invoices.Count;
        int itemsSold = invoices.Sum(i => i.Items.Sum(it => it.Quantity));

        decimal totalCost = invoices.Sum(i => i.Items.Sum(it => it.CostPrice * it.Quantity));
        decimal grossProfit = totalRevenue - totalCost;
        decimal margin = totalRevenue > 0 ? (grossProfit / totalRevenue) * 100m : 0;

        decimal cash = invoices.Where(i => i.PaymentMethod.Equals("Cash", StringComparison.OrdinalIgnoreCase)).Sum(i => i.TotalAmount);
        decimal card = invoices.Where(i => i.PaymentMethod.Equals("Card", StringComparison.OrdinalIgnoreCase)).Sum(i => i.TotalAmount);
        decimal upi = invoices.Where(i => i.PaymentMethod.Equals("UPI", StringComparison.OrdinalIgnoreCase)).Sum(i => i.TotalAmount);

        return new SalesSummary(
            totalRevenue,
            totalInvoices,
            totalCost,
            grossProfit,
            margin,
            cash,
            card,
            upi,
            itemsSold
        );
    }

    public async Task<List<SizeSalesStat>> GetTopSellingSizesAsync(DateTime fromDate, DateTime toDate, int top = 8)
    {
        var start = fromDate.Date;
        var end = toDate.Date.AddDays(1).AddTicks(-1);

        var items = await _context.InvoiceItems
            .Include(it => it.Invoice)
            .Where(it => it.Invoice.InvoiceDate >= start && it.Invoice.InvoiceDate <= end && !string.IsNullOrEmpty(it.Size))
            .ToListAsync();

        return items
            .GroupBy(it => it.Size.Trim().ToUpperInvariant())
            .Select(g => new SizeSalesStat(g.Key, g.Sum(x => x.Quantity), g.Sum(x => x.TotalPrice)))
            .OrderByDescending(s => s.QuantitySold)
            .Take(top)
            .ToList();
    }

    public async Task<List<CategorySalesStat>> GetTopSellingCategoriesAsync(DateTime fromDate, DateTime toDate)
    {
        var start = fromDate.Date;
        var end = toDate.Date.AddDays(1).AddTicks(-1);

        var items = await _context.InvoiceItems
            .Include(it => it.Invoice)
            .Include(it => it.Product)
            .Where(it => it.Invoice.InvoiceDate >= start && it.Invoice.InvoiceDate <= end)
            .ToListAsync();

        return items
            .GroupBy(it => it.Product?.Category ?? "General")
            .Select(g => new CategorySalesStat(g.Key, g.Sum(x => x.Quantity), g.Sum(x => x.TotalPrice)))
            .OrderByDescending(s => s.Revenue)
            .ToList();
    }

    public async Task<List<Invoice>> GetInvoicesAsync(DateTime fromDate, DateTime toDate, string? search = null)
    {
        var start = fromDate.Date;
        var end = toDate.Date.AddDays(1).AddTicks(-1);

        var q = _context.Invoices
            .Include(i => i.Items)
            .Where(i => i.InvoiceDate >= start && i.InvoiceDate <= end);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(i => i.InvoiceNumber.Contains(s) || 
                             i.CustomerName.Contains(s) || 
                             i.CustomerPhone.Contains(s));
        }

        return await q.OrderByDescending(i => i.InvoiceDate).ToListAsync();
    }

    public async Task<Invoice?> GetInvoiceByIdAsync(int id)
    {
        return await _context.Invoices
            .Include(i => i.Items)
            .FirstOrDefaultAsync(i => i.Id == id);
    }
}
