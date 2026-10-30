using ApparelPOS.Models;

namespace ApparelPOS.Services;

public record ScanResult(Product Product, ProductVariant? Variant);

public interface IBillingService
{
    Task<ScanResult?> FindItemByBarcodeOrSkuAsync(string code);
    Task<string> GenerateNextInvoiceNumberAsync();
    Task<Invoice> ProcessCheckoutAsync(Invoice invoice);
    Task<StoreSettings> GetStoreSettingsAsync();
    Task<StoreSettings> SaveStoreSettingsAsync(StoreSettings settings);
}
