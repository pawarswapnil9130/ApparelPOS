using System.Drawing;
using System.Drawing.Printing;
using System.Text;
using ApparelPOS.Models;

namespace ApparelPOS.Services;

public class PrinterService : IPrinterService
{
    private readonly IBarcodeService _barcodeService;

    public PrinterService(IBarcodeService barcodeService)
    {
        _barcodeService = barcodeService;
    }

    public const string NonePrinterOption = "None / Digital Receipt & PDF Preview (Default)";

    public List<string> GetInstalledPrinters()
    {
        var printers = new List<string> { NonePrinterOption };
        try
        {
            foreach (string printer in PrinterSettings.InstalledPrinters)
            {
                if (!printers.Contains(printer, StringComparer.OrdinalIgnoreCase))
                {
                    printers.Add(printer);
                }
            }
        }
        catch
        {
            // Ignore if Windows printer spooler is stopped
        }
        return printers;
    }

    public string GetDefaultPrinterName()
    {
        var settings = new PrinterSettings();
        return settings.PrinterName;
    }

    public string GenerateReceiptText(Invoice invoice, StoreSettings settings, int charWidth = 42)
    {
        var sb = new StringBuilder();
        var line = new string('-', charWidth);
        var doubleLine = new string('=', charWidth);

        // Header
        sb.AppendLine(Center(settings.StoreName.ToUpperInvariant(), charWidth));
        if (!string.IsNullOrWhiteSpace(settings.Tagline))
            sb.AppendLine(Center(settings.Tagline, charWidth));
        if (!string.IsNullOrWhiteSpace(settings.AddressLine1))
            sb.AppendLine(Center(settings.AddressLine1, charWidth));
        if (!string.IsNullOrWhiteSpace(settings.CityStateZip))
            sb.AppendLine(Center(settings.CityStateZip, charWidth));
        if (!string.IsNullOrWhiteSpace(settings.Phone))
            sb.AppendLine(Center($"Ph: {settings.Phone}", charWidth));
        if (!string.IsNullOrWhiteSpace(settings.GstNumber))
            sb.AppendLine(Center($"GSTIN: {settings.GstNumber}", charWidth));

        sb.AppendLine(doubleLine);

        // Metadata
        sb.AppendLine(PadBetween($"Invoice: {invoice.InvoiceNumber}", $"Date: {invoice.InvoiceDate:dd-MMM-yy}", charWidth));
        sb.AppendLine(PadBetween($"Time: {invoice.InvoiceDate:HH:mm:ss}", $"Cashier: {invoice.CashierName}", charWidth));
        if (!string.IsNullOrWhiteSpace(invoice.CustomerPhone))
        {
            sb.AppendLine(PadBetween($"Customer: {invoice.CustomerName}", $"Ph: {invoice.CustomerPhone}", charWidth));
        }

        sb.AppendLine(line);

        // Items Table Header
        // Layout: Item (Size)      Qty   Rate     Total
        sb.AppendLine(PadColumns("ITEM [SZ]", "QTY", "RATE", "TOTAL", charWidth));
        sb.AppendLine(line);

        foreach (var item in invoice.Items)
        {
            var title = $"{item.ItemName}";
            if (!string.IsNullOrWhiteSpace(item.Size))
            {
                title += $" ({item.Size})";
            }
            if (title.Length > charWidth - 18)
            {
                title = title[..(charWidth - 18)];
            }

            sb.AppendLine(PadColumns(title, item.Quantity.ToString(), $"{item.UnitPrice:0.00}", $"{item.TotalPrice:0.00}", charWidth));
            if (!string.IsNullOrWhiteSpace(item.Color))
            {
                sb.AppendLine($"  Color: {item.Color}");
            }
            if (item.DiscountAmount > 0)
            {
                sb.AppendLine($"  (Disc: -{settings.CurrencySymbol}{item.DiscountAmount:0.00})");
            }
        }

        sb.AppendLine(line);

        // Totals
        sb.AppendLine(PadBetween("Sub Total:", $"{settings.CurrencySymbol}{invoice.SubTotal:0.00}", charWidth));
        if (invoice.DiscountAmount > 0)
        {
            sb.AppendLine(PadBetween($"Discount ({invoice.DiscountPercent:0.#}%):", $"-{settings.CurrencySymbol}{invoice.DiscountAmount:0.00}", charWidth));
        }
        if (invoice.CgstAmount > 0 || invoice.SgstAmount > 0)
        {
            sb.AppendLine(PadBetween("CGST:", $"{settings.CurrencySymbol}{invoice.CgstAmount:0.00}", charWidth));
            sb.AppendLine(PadBetween("SGST:", $"{settings.CurrencySymbol}{invoice.SgstAmount:0.00}", charWidth));
        }
        if (invoice.RoundOff != 0)
        {
            sb.AppendLine(PadBetween("Round Off:", $"{settings.CurrencySymbol}{invoice.RoundOff:+0.00;-0.00;0.00}", charWidth));
        }

        sb.AppendLine(doubleLine);
        sb.AppendLine(PadBetween("GRAND TOTAL:", $"{settings.CurrencySymbol}{invoice.TotalAmount:0.00}", charWidth));
        sb.AppendLine(doubleLine);

        // Payment Info
        sb.AppendLine(PadBetween($"Paid via: {invoice.PaymentMethod}", $"{settings.CurrencySymbol}{invoice.TotalAmount:0.00}", charWidth));
        if (invoice.PaymentMethod.Equals("Cash", StringComparison.OrdinalIgnoreCase))
        {
            sb.AppendLine(PadBetween("Cash Received:", $"{settings.CurrencySymbol}{invoice.CashReceived:0.00}", charWidth));
            sb.AppendLine(PadBetween("Change Returned:", $"{settings.CurrencySymbol}{invoice.ChangeDue:0.00}", charWidth));
        }

        sb.AppendLine(line);

        // Footer
        if (!string.IsNullOrWhiteSpace(settings.ReceiptFooter))
        {
            foreach (var footerLine in settings.ReceiptFooter.Split('\n'))
            {
                sb.AppendLine(Center(footerLine.Trim(), charWidth));
            }
        }

        return sb.ToString();
    }

    public void PrintReceipt(Invoice invoice, StoreSettings settings, string? targetPrinterName = null)
    {
        var is80mm = settings.ThermalPaperWidth == "80mm";
        var charWidth = is80mm ? 42 : 32;
        var paperWidthHundredths = is80mm ? 315 : 228; // ~80mm (3.15") or ~58mm (2.28")

        var printer = !string.IsNullOrWhiteSpace(targetPrinterName) 
            ? targetPrinterName 
            : settings.ThermalPrinterName;

        // If digital preview mode or no printer configured, skip physical printing
        if (string.IsNullOrWhiteSpace(printer) || 
            printer.StartsWith("None", StringComparison.OrdinalIgnoreCase) || 
            printer.Contains("PDF Preview", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var doc = new PrintDocument();
        doc.PrinterSettings.PrinterName = printer;
        doc.DefaultPageSettings.PaperSize = new PaperSize("ThermalReceipt", paperWidthHundredths, 1200);
        doc.DefaultPageSettings.Margins = new Margins(5, 5, 5, 5);

        var receiptText = GenerateReceiptText(invoice, settings, charWidth);

        doc.PrintPage += (sender, e) =>
        {
            var g = e.Graphics;
            if (g == null) return;

            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
            using var font = new Font("Consolas", is80mm ? 8.5f : 7.5f, FontStyle.Regular);
            using var brush = new SolidBrush(Color.Black);

            float x = 5;
            float y = 5;
            float lineHeight = font.GetHeight(g);

            var lines = receiptText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            foreach (var l in lines)
            {
                g.DrawString(l, font, brush, x, y);
                y += lineHeight;
            }

            // Print UPI QR code at bottom if configured
            if (!string.IsNullOrWhiteSpace(settings.UpiVpa))
            {
                try
                {
                    y += 10;
                    var upiPayload = $"upi://pay?pa={settings.UpiVpa}&pn={Uri.EscapeDataString(settings.UpiPayeeName)}&am={invoice.TotalAmount:0.00}&tn={invoice.InvoiceNumber}&cu=INR";
                    using var qrBmp = _barcodeService.GenerateBarcodeBitmap(upiPayload, 120, 120);
                    float qrX = (paperWidthHundredths - 120) / 2f;
                    g.DrawImage(qrBmp, qrX, y, 120, 120);
                    y += 125;
                    using var smallFont = new Font("Arial", 6.5f, FontStyle.Italic);
                    var scanText = "Scan to pay or verify invoice";
                    var strSize = g.MeasureString(scanText, smallFont);
                    g.DrawString(scanText, smallFont, brush, (paperWidthHundredths - strSize.Width) / 2f, y);
                }
                catch
                {
                    // Fail-safe if QR rendering is skipped
                }
            }

            e.HasMorePages = false;
        };

        doc.Print();
    }

    private static string Center(string text, int width)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        if (text.Length >= width) return text[..width];
        int left = (width - text.Length) / 2;
        return new string(' ', left) + text;
    }

    private static string PadBetween(string left, string right, int width)
    {
        int space = width - left.Length - right.Length;
        if (space < 1) space = 1;
        return left + new string(' ', space) + right;
    }

    private static string PadColumns(string col1, string col2, string col3, string col4, int width)
    {
        // 4 columns: Name, Qty, Rate, Total
        // e.g. for width 42: col1=20, col2=4, col3=8, col4=10
        int c1Width = width - 22;
        if (c1Width < 12) c1Width = 12;

        var s1 = col1.Length > c1Width ? col1[..c1Width] : col1.PadRight(c1Width);
        var s2 = col2.PadLeft(4);
        var s3 = col3.PadLeft(8);
        var s4 = col4.PadLeft(10);

        return $"{s1}{s2}{s3}{s4}";
    }
}
