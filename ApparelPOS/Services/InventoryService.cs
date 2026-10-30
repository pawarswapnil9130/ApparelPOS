using System.Globalization;
using System.IO;
using System.Text;
using ApparelPOS.Data;
using ApparelPOS.Models;
using Microsoft.EntityFrameworkCore;

namespace ApparelPOS.Services;

public class InventoryService : IInventoryService
{
    private readonly AppDbContext _context;

    public InventoryService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<Product>> GetAllProductsAsync()
    {
        return await _context.Products
            .Include(p => p.Variants)
            .OrderBy(p => p.Name)
            .ToListAsync();
    }

    public async Task<List<Product>> SearchProductsAsync(string? query, string? category = null, bool lowStockOnly = false)
    {
        var q = _context.Products.Include(p => p.Variants).AsQueryable();

        if (!string.IsNullOrWhiteSpace(category) && category != "All Categories")
        {
            q = q.Where(p => p.Category == category);
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            var trimmed = query.Trim();
            q = q.Where(p => p.Name.Contains(trimmed) || 
                             p.Sku.Contains(trimmed) || 
                             p.Barcode.Contains(trimmed) ||
                             p.Variants.Any(v => v.BarcodeVariant.Contains(trimmed) || 
                                                 v.SkuVariant.Contains(trimmed) ||
                                                 v.Size.Contains(trimmed) ||
                                                 v.Color.Contains(trimmed)));
        }

        var results = await q.ToListAsync();

        if (lowStockOnly)
        {
            results = results.Where(p => p.IsLowStock).ToList();
        }

        return results;
    }

    public async Task<Product?> GetProductByIdAsync(int id)
    {
        return await _context.Products
            .Include(p => p.Variants)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<Product> SaveProductAsync(Product product)
    {
        product.UpdatedAt = DateTime.Now;

        // Auto-register category in Categories table if not already present
        if (!string.IsNullOrWhiteSpace(product.Category))
        {
            var catName = product.Category.Trim();
            var exists = await _context.Categories.AnyAsync(c => c.Name.ToLower() == catName.ToLower());
            if (!exists)
            {
                _context.Categories.Add(new Category
                {
                    Name = catName,
                    HsnCode = "6205",
                    DefaultTaxRate = product.TaxRate > 0 ? product.TaxRate : 5.0m
                });
            }
        }

        if (product.Id == 0)
        {
            product.CreatedAt = DateTime.Now;
            _context.Products.Add(product);
        }
        else
        {
            _context.Products.Update(product);
        }

        await _context.SaveChangesAsync();
        return product;
    }

    public async Task DeleteProductAsync(int id)
    {
        var product = await _context.Products.Include(p => p.Variants).FirstOrDefaultAsync(p => p.Id == id);
        if (product != null)
        {
            _context.Products.Remove(product);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<ProductVariant?> GetVariantByIdAsync(int variantId)
    {
        return await _context.ProductVariants
            .Include(v => v.Product)
            .FirstOrDefaultAsync(v => v.Id == variantId);
    }

    public async Task<ProductVariant> SaveVariantAsync(ProductVariant variant)
    {
        if (variant.Id == 0)
        {
            _context.ProductVariants.Add(variant);
        }
        else
        {
            _context.ProductVariants.Update(variant);
        }

        await _context.SaveChangesAsync();
        return variant;
    }

    public async Task DeleteVariantAsync(int variantId)
    {
        var variant = await _context.ProductVariants.FindAsync(variantId);
        if (variant != null)
        {
            _context.ProductVariants.Remove(variant);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<List<Category>> GetCategoriesAsync()
    {
        var existingCategories = await _context.Categories.OrderBy(c => c.Name).ToListAsync();
        var productCategories = await _context.Products
            .Where(p => !string.IsNullOrEmpty(p.Category))
            .Select(p => p.Category)
            .Distinct()
            .ToListAsync();

        var existingNames = new HashSet<string>(existingCategories.Select(c => c.Name), StringComparer.OrdinalIgnoreCase);
        bool addedAny = false;

        foreach (var pCat in productCategories)
        {
            var trimmed = pCat.Trim();
            if (!string.IsNullOrEmpty(trimmed) && !existingNames.Contains(trimmed))
            {
                var newCat = new Category { Name = trimmed, HsnCode = "6205", DefaultTaxRate = 5.0m };
                _context.Categories.Add(newCat);
                existingCategories.Add(newCat);
                existingNames.Add(trimmed);
                addedAny = true;
            }
        }

        if (addedAny)
        {
            await _context.SaveChangesAsync();
        }

        return existingCategories.OrderBy(c => c.Name).ToList();
    }

    public async Task<Category> AddCategoryAsync(string name, string hsn, decimal taxRate)
    {
        var cat = new Category
        {
            Name = name,
            HsnCode = hsn,
            DefaultTaxRate = taxRate
        };
        _context.Categories.Add(cat);
        await _context.SaveChangesAsync();
        return cat;
    }

    public async Task<int> ExportToCsvAsync(string filePath)
    {
        var products = await _context.Products.Include(p => p.Variants).ToListAsync();
        var sb = new StringBuilder();
        
        // Header
        sb.AppendLine("ProductSku,ProductName,Category,Description,CostPrice,SellingPrice,TaxRate,LowStockThreshold,VariantSku,VariantBarcode,Size,Color,Fabric,FitType,StockQuantity,AdditionalPrice");

        int count = 0;
        foreach (var p in products)
        {
            if (p.Variants.Any())
            {
                foreach (var v in p.Variants)
                {
                    sb.AppendLine(string.Join(",",
                        EscapeCsv(p.Sku),
                        EscapeCsv(p.Name),
                        EscapeCsv(p.Category),
                        EscapeCsv(p.Description ?? ""),
                        p.CostPrice.ToString("F2", CultureInfo.InvariantCulture),
                        p.SellingPrice.ToString("F2", CultureInfo.InvariantCulture),
                        p.TaxRate.ToString("F2", CultureInfo.InvariantCulture),
                        p.LowStockThreshold,
                        EscapeCsv(v.SkuVariant),
                        EscapeCsv(v.BarcodeVariant),
                        EscapeCsv(v.Size),
                        EscapeCsv(v.Color),
                        EscapeCsv(v.Fabric),
                        EscapeCsv(v.FitType),
                        v.StockQuantity,
                        v.AdditionalPrice.ToString("F2", CultureInfo.InvariantCulture)
                    ));
                    count++;
                }
            }
            else
            {
                sb.AppendLine(string.Join(",",
                    EscapeCsv(p.Sku),
                    EscapeCsv(p.Name),
                    EscapeCsv(p.Category),
                    EscapeCsv(p.Description ?? ""),
                    p.CostPrice.ToString("F2", CultureInfo.InvariantCulture),
                    p.SellingPrice.ToString("F2", CultureInfo.InvariantCulture),
                    p.TaxRate.ToString("F2", CultureInfo.InvariantCulture),
                    p.LowStockThreshold,
                    "", "", "", "", "", "", 0, "0.00"
                ));
                count++;
            }
        }

        await File.WriteAllTextAsync(filePath, sb.ToString(), Encoding.UTF8);
        return count;
    }

    public async Task<(int imported, int updated, List<string> errors)> ImportFromCsvAsync(string filePath)
    {
        var errors = new List<string>();
        int imported = 0;
        int updated = 0;

        if (!File.Exists(filePath))
        {
            errors.Add("Selected CSV file does not exist.");
            return (0, 0, errors);
        }

        var lines = await File.ReadAllLinesAsync(filePath);
        if (lines.Length <= 1)
        {
            errors.Add("CSV file is empty or missing data rows.");
            return (0, 0, errors);
        }

        // Header: ProductSku,ProductName,Category,Description,CostPrice,SellingPrice,TaxRate,LowStockThreshold,VariantSku,VariantBarcode,Size,Color,Fabric,FitType,StockQuantity,AdditionalPrice
        for (int i = 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;

            var cols = ParseCsvLine(line);
            if (cols.Count < 7)
            {
                errors.Add($"Line {i + 1}: Insufficient columns.");
                continue;
            }

            try
            {
                var sku = cols[0].Trim();
                var name = cols[1].Trim();
                var cat = cols[2].Trim();
                var desc = cols[3].Trim();
                decimal.TryParse(cols[4], NumberStyles.Any, CultureInfo.InvariantCulture, out var cost);
                decimal.TryParse(cols[5], NumberStyles.Any, CultureInfo.InvariantCulture, out var mrp);
                decimal.TryParse(cols[6], NumberStyles.Any, CultureInfo.InvariantCulture, out var tax);
                int.TryParse(cols.Count > 7 ? cols[7] : "5", out var threshold);

                if (string.IsNullOrWhiteSpace(sku) || string.IsNullOrWhiteSpace(name))
                {
                    errors.Add($"Line {i + 1}: SKU and Name are required.");
                    continue;
                }

                var product = await _context.Products.Include(p => p.Variants).FirstOrDefaultAsync(p => p.Sku == sku);
                bool isNew = false;
                if (product == null)
                {
                    product = new Product
                    {
                        Sku = sku,
                        Barcode = cols.Count > 9 && !string.IsNullOrWhiteSpace(cols[9]) ? cols[9] : sku,
                        Name = name,
                        Category = cat,
                        Description = desc,
                        CostPrice = cost,
                        SellingPrice = mrp,
                        TaxRate = tax > 0 ? tax : 5.0m,
                        LowStockThreshold = threshold > 0 ? threshold : 5,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    };
                    _context.Products.Add(product);
                    isNew = true;
                }
                else
                {
                    product.Name = name;
                    product.Category = cat;
                    product.CostPrice = cost;
                    product.SellingPrice = mrp;
                    product.TaxRate = tax;
                    product.LowStockThreshold = threshold;
                    product.UpdatedAt = DateTime.Now;
                }

                // Check for variant columns
                if (cols.Count >= 15 && !string.IsNullOrWhiteSpace(cols[8])) // VariantSku
                {
                    var varSku = cols[8].Trim();
                    var varBarcode = cols[9].Trim();
                    var size = cols[10].Trim();
                    var color = cols[11].Trim();
                    var fabric = cols[12].Trim();
                    var fit = cols[13].Trim();
                    int.TryParse(cols[14], out var stock);
                    decimal.TryParse(cols.Count > 15 ? cols[15] : "0", NumberStyles.Any, CultureInfo.InvariantCulture, out var addPrice);

                    var existingVar = product.Variants.FirstOrDefault(v => v.SkuVariant == varSku || v.BarcodeVariant == varBarcode);
                    if (existingVar == null)
                    {
                        product.Variants.Add(new ProductVariant
                        {
                            SkuVariant = varSku,
                            BarcodeVariant = string.IsNullOrWhiteSpace(varBarcode) ? varSku : varBarcode,
                            Size = size,
                            Color = color,
                            Fabric = fabric,
                            FitType = fit,
                            StockQuantity = stock,
                            AdditionalPrice = addPrice
                        });
                    }
                    else
                    {
                        existingVar.Size = size;
                        existingVar.Color = color;
                        existingVar.Fabric = fabric;
                        existingVar.FitType = fit;
                        existingVar.StockQuantity = stock;
                        existingVar.AdditionalPrice = addPrice;
                    }
                }

                if (isNew) imported++; else updated++;
            }
            catch (Exception ex)
            {
                errors.Add($"Line {i + 1}: Error parsing row: {ex.Message}");
            }
        }

        await _context.SaveChangesAsync();
        return (imported, updated, errors);
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return value;
    }

    private static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }
        result.Add(current.ToString());
        return result;
    }
}
