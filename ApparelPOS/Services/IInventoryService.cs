using ApparelPOS.Models;

namespace ApparelPOS.Services;

public interface IInventoryService
{
    Task<List<Product>> GetAllProductsAsync();
    Task<List<Product>> SearchProductsAsync(string? query, string? category = null, bool lowStockOnly = false);
    Task<Product?> GetProductByIdAsync(int id);
    Task<Product> SaveProductAsync(Product product);
    Task DeleteProductAsync(int id);
    
    Task<ProductVariant?> GetVariantByIdAsync(int variantId);
    Task<ProductVariant> SaveVariantAsync(ProductVariant variant);
    Task DeleteVariantAsync(int variantId);

    Task<List<Category>> GetCategoriesAsync();
    Task<Category> AddCategoryAsync(string name, string hsn, decimal taxRate);

    Task<int> ExportToCsvAsync(string filePath);
    Task<(int imported, int updated, List<string> errors)> ImportFromCsvAsync(string filePath);
}
