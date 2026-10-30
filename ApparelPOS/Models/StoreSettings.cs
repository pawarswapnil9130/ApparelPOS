namespace ApparelPOS.Models;

public class StoreSettings
{
    public int Id { get; set; } = 1;
    public string StoreName { get; set; } = "Velvet Thread Apparel Co.";
    public string AddressLine1 { get; set; } = "Shop #14, Fashion Avenue, MG Road";
    public string CityStateZip { get; set; } = "Mumbai, Maharashtra 400001";
    public string Phone { get; set; } = "+91 98765 43210";
    public string Email { get; set; } = "contact@velvetthread.in";
    public string GstNumber { get; set; } = "27AAAAA0000A1Z5";
    public string Tagline { get; set; } = "Premium Clothing & Fashion";
    public string ReceiptFooter { get; set; } = "Thank you for shopping with us!\nExchange within 7 days with original bill & tags intact.";
    public string CurrencySymbol { get; set; } = "₹";
    
    // Offline UPI static QR configuration
    public string UpiVpa { get; set; } = "velvetthread@okaxis";
    public string UpiPayeeName { get; set; } = "Velvet Thread Apparel";

    // Hardware Printing Settings
    public string ThermalPaperWidth { get; set; } = "80mm"; // 58mm or 80mm
    public string? ThermalPrinterName { get; set; }
    public string? LabelPrinterName { get; set; }
    public bool AutoCutReceipt { get; set; } = true;
    public bool OpenCashDrawer { get; set; } = true;
}
