using System.ComponentModel.DataAnnotations.Schema;

namespace ApparelPOS.Models;

public class ProductVariant
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    
    // Apparel Matrix Dimensions
    public string Size { get; set; } = "M"; // S, M, L, XL, XXL, 30, 32, 34, 38, 40, 42
    public string Color { get; set; } = "Navy Blue";
    public string Fabric { get; set; } = "100% Cotton";
    public string FitType { get; set; } = "Slim Fit"; // Slim Fit, Regular Fit, Relaxed Fit
    
    public string SkuVariant { get; set; } = string.Empty;
    public string BarcodeVariant { get; set; } = string.Empty; // Scanned by USB Barcode Scanner
    
    public decimal AdditionalPrice { get; set; } = 0.0m;
    public int StockQuantity { get; set; } = 10;
    
    public Product Product { get; set; } = null!;

    [NotMapped]
    public decimal EffectivePrice => (Product?.SellingPrice ?? 0m) + AdditionalPrice;

    [NotMapped]
    public string DisplayName => $"{Product?.Name} ({Size} / {Color} / {FitType})";

    [NotMapped]
    public bool IsLowStock => StockQuantity <= (Product?.LowStockThreshold ?? 5);
}
