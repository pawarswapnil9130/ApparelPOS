using ApparelPOS.Data;
using ApparelPOS.Models;
using Microsoft.EntityFrameworkCore;

namespace ApparelPOS.Services;

public class BillingService : IBillingService
{
    private readonly AppDbContext _context;

    public BillingService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ScanResult?> FindItemByBarcodeOrSkuAsync(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;

        var clean = code.Trim();
        var candidates = new List<string> { clean };

        if (clean.All(char.IsDigit))
        {
            var trimmedZeros = clean.TrimStart('0');
            if (!string.IsNullOrEmpty(trimmedZeros) && !candidates.Contains(trimmedZeros))
            {
                candidates.Add(trimmedZeros);
            }

            if (clean.Length == 12)
            {
                var ean13 = "0" + clean;
                if (!candidates.Contains(ean13))
                {
                    candidates.Add(ean13);
                }
            }
        }

        // 1. Check exact / case-insensitive match in ProductVariants (Variant Barcode or Variant SKU)
        foreach (var candidate in candidates)
        {
            var variant = await _context.ProductVariants
                .Include(v => v.Product)
                .FirstOrDefaultAsync(v =>
                    (v.BarcodeVariant != null && (v.BarcodeVariant == candidate || EF.Functions.Like(v.BarcodeVariant, candidate))) ||
                    (v.SkuVariant != null && (v.SkuVariant == candidate || EF.Functions.Like(v.SkuVariant, candidate))));

            if (variant != null && variant.Product != null)
            {
                return new ScanResult(variant.Product, variant);
            }
        }

        // 2. Check Master Product (Barcode or SKU)
        foreach (var candidate in candidates)
        {
            var product = await _context.Products
                .Include(p => p.Variants)
                .FirstOrDefaultAsync(p =>
                    (p.Barcode != null && (p.Barcode == candidate || EF.Functions.Like(p.Barcode, candidate))) ||
                    (p.Sku != null && (p.Sku == candidate || EF.Functions.Like(p.Sku, candidate))));

            if (product != null)
            {
                var defaultVariant = product.Variants.FirstOrDefault();
                return new ScanResult(product, defaultVariant);
            }
        }

        return null;
    }

    public async Task<string> GenerateNextInvoiceNumberAsync()
    {
        var todayPrefix = $"INV-{DateTime.Today:yyyyMMdd}-";
        
        var lastInvoice = await _context.Invoices
            .Where(i => i.InvoiceNumber.StartsWith(todayPrefix))
            .OrderByDescending(i => i.Id)
            .Select(i => i.InvoiceNumber)
            .FirstOrDefaultAsync();

        int nextSeq = 1;
        if (!string.IsNullOrEmpty(lastInvoice))
        {
            var parts = lastInvoice.Split('-');
            if (parts.Length == 3 && int.TryParse(parts[2], out var seq))
            {
                nextSeq = seq + 1;
            }
        }

        return $"{todayPrefix}{nextSeq:D4}";
    }

    public async Task<Invoice> ProcessCheckoutAsync(Invoice invoice)
    {
        if (invoice.Items == null || !invoice.Items.Any())
        {
            throw new InvalidOperationException("Cannot checkout an invoice with no items.");
        }

        if (string.IsNullOrWhiteSpace(invoice.InvoiceNumber))
        {
            invoice.InvoiceNumber = await GenerateNextInvoiceNumberAsync();
        }

        invoice.InvoiceDate = DateTime.Now;

        // Transaction for ACID atomic checkout
        using var tx = await _context.Database.BeginTransactionAsync();
        try
        {
            // Reduce inventory stock for each variant
            foreach (var item in invoice.Items)
            {
                if (item.VariantId.HasValue && item.VariantId.Value > 0)
                {
                    var variant = await _context.ProductVariants.FindAsync(item.VariantId.Value);
                    if (variant != null)
                    {
                        variant.StockQuantity -= item.Quantity;
                        if (variant.StockQuantity < 0)
                        {
                            // We allow negative stock with warning in retail, or keep it tracked
                        }
                    }
                }
            }

            _context.Invoices.Add(invoice);
            await _context.SaveChangesAsync();
            await tx.CommitAsync();

            return invoice;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<StoreSettings> GetStoreSettingsAsync()
    {
        var settings = await _context.StoreSettings.FirstOrDefaultAsync();
        if (settings == null)
        {
            settings = new StoreSettings();
            _context.StoreSettings.Add(settings);
            await _context.SaveChangesAsync();
        }
        return settings;
    }

    public async Task<StoreSettings> SaveStoreSettingsAsync(StoreSettings settings)
    {
        if (settings.Id == 0)
        {
            _context.StoreSettings.Add(settings);
        }
        else
        {
            _context.StoreSettings.Update(settings);
        }
        await _context.SaveChangesAsync();
        return settings;
    }
}
