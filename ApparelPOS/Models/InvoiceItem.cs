namespace ApparelPOS.Models;

public class InvoiceItem
{
    public int Id { get; set; }
    public int InvoiceId { get; set; }
    public int ProductId { get; set; }
    public int? VariantId { get; set; }

    public string ItemName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string Barcode { get; set; } = string.Empty;
    
    public string Size { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;

    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public decimal CostPrice { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal DiscountAmount { get; set; }
    
    public decimal TaxRate { get; set; } = 5.0m;
    public decimal TaxAmount { get; set; }
    public decimal TotalPrice { get; set; }

    public Invoice Invoice { get; set; } = null!;
    public Product Product { get; set; } = null!;
    public ProductVariant? Variant { get; set; }
}
