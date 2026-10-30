using System.ComponentModel.DataAnnotations.Schema;

namespace ApparelPOS.Models;

public class Product
{
    public int Id { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Barcode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = "Shirts";
    public string? Description { get; set; }
    public decimal CostPrice { get; set; }
    public decimal SellingPrice { get; set; } // MRP
    public decimal TaxRate { get; set; } = 5.0m; // 5% GST
    public int LowStockThreshold { get; set; } = 5;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    public ICollection<ProductVariant> Variants { get; set; } = new List<ProductVariant>();

    [NotMapped]
    public int TotalStock => Variants?.Sum(v => v.StockQuantity) ?? 0;

    [NotMapped]
    public bool IsLowStock => TotalStock <= LowStockThreshold;
}
