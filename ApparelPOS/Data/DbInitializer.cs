using ApparelPOS.Models;
using Microsoft.EntityFrameworkCore;

namespace ApparelPOS.Data;

public static class DbInitializer
{
    public static void Initialize(AppDbContext context)
    {
        // Ensure database file and schema are created
        context.Database.EnsureCreated();
        context.EnableWalMode();

        // Seed Store Settings
        if (!context.StoreSettings.Any())
        {
            context.StoreSettings.Add(new StoreSettings
            {
                StoreName = "Velvet Thread Apparel Co.",
                AddressLine1 = "Shop #14, Fashion Avenue, MG Road",
                CityStateZip = "Mumbai, Maharashtra 400001",
                Phone = "+91 98765 43210",
                Email = "contact@velvetthread.in",
                GstNumber = "27AAAAA0000A1Z5",
                Tagline = "Premium Menswear & Casual Apparel",
                ReceiptFooter = "Thank you for shopping at Velvet Thread!\nExchange within 7 days with tag and bill intact.\nVisit again!",
                CurrencySymbol = "₹",
                UpiVpa = "velvetthread@okaxis",
                UpiPayeeName = "Velvet Thread Apparel",
                ThermalPaperWidth = "80mm",
                AutoCutReceipt = true
            });
            context.SaveChanges();
        }

        // Seed Categories
        if (!context.Categories.Any())
        {
            var categories = new List<Category>
            {
                new() { Name = "Formal Shirts", HsnCode = "6205", DefaultTaxRate = 5.0m },
                new() { Name = "Casual Shirts", HsnCode = "6205", DefaultTaxRate = 5.0m },
                new() { Name = "Denim Jeans", HsnCode = "6203", DefaultTaxRate = 5.0m },
                new() { Name = "Cotton Trousers & Chinos", HsnCode = "6203", DefaultTaxRate = 5.0m },
                new() { Name = "Polo T-Shirts", HsnCode = "6105", DefaultTaxRate = 5.0m },
                new() { Name = "Ethnic Kurtas", HsnCode = "6205", DefaultTaxRate = 5.0m },
                new() { Name = "Blazers & Jackets", HsnCode = "6201", DefaultTaxRate = 12.0m },
                new() { Name = "Accessories", HsnCode = "6217", DefaultTaxRate = 12.0m }
            };

            context.Categories.AddRange(categories);
            context.SaveChanges();
        }

        // Seed Products and Variants
        if (!context.Products.Any())
        {
            SeedSampleProducts(context);
        }
    }

    public static void SeedSampleProducts(AppDbContext context)
    {
        var existingSkus = new HashSet<string>(context.Products.Select(p => p.Sku));
        var samples = GetSampleProducts();
        foreach (var p in samples)
        {
            if (!existingSkus.Contains(p.Sku))
            {
                context.Products.Add(p);
            }
        }
        context.SaveChanges();
    }

    public static List<Product> GetSampleProducts()
    {
        var p1 = new Product
        {
            Name = "Oxford Classic Button-Down Shirt",
            Category = "Formal Shirts",
            Sku = "SHIRT-OXF-01",
            Barcode = "8901234560010",
            Description = "100% Egyptian Giza Cotton formal button-down shirt",
            CostPrice = 750.00m,
            SellingPrice = 1499.00m,
            TaxRate = 5.0m,
            LowStockThreshold = 4,
            Variants = new List<ProductVariant>
            {
                new() { Size = "38 (S)", Color = "Sky Blue", Fabric = "Giza Cotton", FitType = "Slim Fit", SkuVariant = "SHIRT-OXF-01-BLU-38", BarcodeVariant = "8901234560011", StockQuantity = 8 },
                new() { Size = "40 (M)", Color = "Sky Blue", Fabric = "Giza Cotton", FitType = "Slim Fit", SkuVariant = "SHIRT-OXF-01-BLU-40", BarcodeVariant = "8901234560012", StockQuantity = 14 },
                new() { Size = "42 (L)", Color = "Sky Blue", Fabric = "Giza Cotton", FitType = "Slim Fit", SkuVariant = "SHIRT-OXF-01-BLU-42", BarcodeVariant = "8901234560013", StockQuantity = 12 },
                new() { Size = "40 (M)", Color = "Crisp White", Fabric = "Giza Cotton", FitType = "Regular Fit", SkuVariant = "SHIRT-OXF-01-WHT-40", BarcodeVariant = "8901234560014", StockQuantity = 15 },
                new() { Size = "42 (L)", Color = "Crisp White", Fabric = "Giza Cotton", FitType = "Regular Fit", SkuVariant = "SHIRT-OXF-01-WHT-42", BarcodeVariant = "8901234560015", StockQuantity = 9 },
                new() { Size = "44 (XL)", Color = "Crisp White", Fabric = "Giza Cotton", FitType = "Regular Fit", SkuVariant = "SHIRT-OXF-01-WHT-44", BarcodeVariant = "8901234560016", StockQuantity = 3 }
            }
        };

        var p2 = new Product
        {
            Name = "Selvedge Stretch Denim Jeans",
            Category = "Denim Jeans",
            Sku = "JEAN-SLV-02",
            Barcode = "8901234560020",
            Description = "Premium 13oz comfort-stretch raw indigo denim",
            CostPrice = 1100.00m,
            SellingPrice = 2299.00m,
            TaxRate = 5.0m,
            LowStockThreshold = 5,
            Variants = new List<ProductVariant>
            {
                new() { Size = "30", Color = "Dark Indigo", Fabric = "98% Cotton 2% Elastane", FitType = "Slim Straight", SkuVariant = "JEAN-SLV-02-IND-30", BarcodeVariant = "8901234560021", StockQuantity = 6 },
                new() { Size = "32", Color = "Dark Indigo", Fabric = "98% Cotton 2% Elastane", FitType = "Slim Straight", SkuVariant = "JEAN-SLV-02-IND-32", BarcodeVariant = "8901234560022", StockQuantity = 18 },
                new() { Size = "34", Color = "Dark Indigo", Fabric = "98% Cotton 2% Elastane", FitType = "Slim Straight", SkuVariant = "JEAN-SLV-02-IND-34", BarcodeVariant = "8901234560023", StockQuantity = 11 },
                new() { Size = "32", Color = "Washed Black", Fabric = "98% Cotton 2% Elastane", FitType = "Tapered Fit", SkuVariant = "JEAN-SLV-02-BLK-32", BarcodeVariant = "8901234560024", StockQuantity = 9 },
                new() { Size = "34", Color = "Washed Black", Fabric = "98% Cotton 2% Elastane", FitType = "Tapered Fit", SkuVariant = "JEAN-SLV-02-BLK-34", BarcodeVariant = "8901234560025", StockQuantity = 4 }
            }
        };

        var p3 = new Product
        {
            Name = "Supima Cotton Polo T-Shirt",
            Category = "Polo T-Shirts",
            Sku = "POLO-SUP-03",
            Barcode = "8901234560030",
            Description = "Signature honey-comb pique knit breathable polo",
            CostPrice = 420.00m,
            SellingPrice = 899.00m,
            TaxRate = 5.0m,
            LowStockThreshold = 5,
            Variants = new List<ProductVariant>
            {
                new() { Size = "M", Color = "Navy Blue", Fabric = "Supima Pique", FitType = "Athletic Fit", SkuVariant = "POLO-SUP-03-NVY-M", BarcodeVariant = "8901234560031", StockQuantity = 20 },
                new() { Size = "L", Color = "Navy Blue", Fabric = "Supima Pique", FitType = "Athletic Fit", SkuVariant = "POLO-SUP-03-NVY-L", BarcodeVariant = "8901234560032", StockQuantity = 16 },
                new() { Size = "XL", Color = "Navy Blue", Fabric = "Supima Pique", FitType = "Athletic Fit", SkuVariant = "POLO-SUP-03-NVY-XL", BarcodeVariant = "8901234560033", StockQuantity = 8 },
                new() { Size = "M", Color = "Burgundy Wine", Fabric = "Supima Pique", FitType = "Athletic Fit", SkuVariant = "POLO-SUP-03-BUR-M", BarcodeVariant = "8901234560034", StockQuantity = 12 },
                new() { Size = "L", Color = "Burgundy Wine", Fabric = "Supima Pique", FitType = "Athletic Fit", SkuVariant = "POLO-SUP-03-BUR-L", BarcodeVariant = "8901234560035", StockQuantity = 10 },
                new() { Size = "M", Color = "Forest Olive", Fabric = "Supima Pique", FitType = "Athletic Fit", SkuVariant = "POLO-SUP-03-OLV-M", BarcodeVariant = "8901234560036", StockQuantity = 2 }
            }
        };

        var p4 = new Product
        {
            Name = "Pure Linen Mandarin Collar Kurta",
            Category = "Ethnic Kurtas",
            Sku = "KURTA-LIN-04",
            Barcode = "8901234560040",
            Description = "Breathable French flax linen festive short kurta",
            CostPrice = 900.00m,
            SellingPrice = 1899.00m,
            TaxRate = 5.0m,
            LowStockThreshold = 3,
            Variants = new List<ProductVariant>
            {
                new() { Size = "38 (M)", Color = "Natural Beige", Fabric = "Pure Linen", FitType = "Comfort Fit", SkuVariant = "KURTA-LIN-04-BGE-38", BarcodeVariant = "8901234560041", StockQuantity = 7 },
                new() { Size = "40 (L)", Color = "Natural Beige", Fabric = "Pure Linen", FitType = "Comfort Fit", SkuVariant = "KURTA-LIN-04-BGE-40", BarcodeVariant = "8901234560042", StockQuantity = 9 },
                new() { Size = "42 (XL)", Color = "Natural Beige", Fabric = "Pure Linen", FitType = "Comfort Fit", SkuVariant = "KURTA-LIN-04-BGE-42", BarcodeVariant = "8901234560043", StockQuantity = 5 },
                new() { Size = "40 (L)", Color = "Royal Emerald", Fabric = "Pure Linen", FitType = "Comfort Fit", SkuVariant = "KURTA-LIN-04-EMR-40", BarcodeVariant = "8901234560044", StockQuantity = 1 }
            }
        };

        var p5 = new Product
        {
            Name = "Italian Cut Structured Blazer",
            Category = "Blazers & Jackets",
            Sku = "BLAZER-ITL-05",
            Barcode = "8901234560050",
            Description = "Premium wool-blend tailored single-breasted evening blazer",
            CostPrice = 2800.00m,
            SellingPrice = 5499.00m,
            TaxRate = 12.0m,
            LowStockThreshold = 2,
            Variants = new List<ProductVariant>
            {
                new() { Size = "38", Color = "Midnight Navy", Fabric = "Wool Blend", FitType = "Italian Tailored", SkuVariant = "BLAZER-ITL-05-NVY-38", BarcodeVariant = "8901234560051", StockQuantity = 4 },
                new() { Size = "40", Color = "Midnight Navy", Fabric = "Wool Blend", FitType = "Italian Tailored", SkuVariant = "BLAZER-ITL-05-NVY-40", BarcodeVariant = "8901234560052", StockQuantity = 5 },
                new() { Size = "42", Color = "Midnight Navy", Fabric = "Wool Blend", FitType = "Italian Tailored", SkuVariant = "BLAZER-ITL-05-NVY-42", BarcodeVariant = "8901234560053", StockQuantity = 3 }
            }
        };

        return new List<Product> { p1, p2, p3, p4, p5 };
    }
}
