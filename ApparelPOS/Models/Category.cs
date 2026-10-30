namespace ApparelPOS.Models;

public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string HsnCode { get; set; } = "6205"; // Default apparel HSN
    public decimal DefaultTaxRate { get; set; } = 5.0m; // 5% GST standard apparel

    public ICollection<Product> Products { get; set; } = new List<Product>();
}
