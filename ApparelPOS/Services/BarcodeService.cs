using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Printing;
using System.IO;
using System.Windows.Media.Imaging;
using ApparelPOS.Models;
using ZXing;
using ZXing.Common;
using ZXing.Windows.Compatibility;

namespace ApparelPOS.Services;

public class BarcodeService : IBarcodeService
{
    public Bitmap GenerateBarcodeBitmap(string content, int width = 320, int height = 100)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            content = "000000000000";
        }

        var writer = new BarcodeWriter
        {
            Format = BarcodeFormat.CODE_128,
            Options = new EncodingOptions
            {
                Width = width,
                Height = height,
                Margin = 2,
                PureBarcode = false
            }
        };

        return writer.Write(content);
    }

    public BitmapSource? GenerateBarcodeImageSource(string content, int width = 320, int height = 100)
    {
        try
        {
            using var bmp = GenerateBarcodeBitmap(content, width, height);
            return ToBitmapSource(bmp);
        }
        catch
        {
            return null;
        }
    }

    public BitmapSource? GenerateQrCodeImageSource(string content, int size = 240)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return null;
            }

            var writer = new BarcodeWriter
            {
                Format = BarcodeFormat.QR_CODE,
                Options = new EncodingOptions
                {
                    Width = size,
                    Height = size,
                    Margin = 1
                }
            };

            using var bmp = writer.Write(content);
            return ToBitmapSource(bmp);
        }
        catch
        {
            return null;
        }
    }

    public string GenerateTsplCommands(Product product, ProductVariant variant, StoreSettings settings, int copies = 1)
    {
        var barcode = !string.IsNullOrWhiteSpace(variant.BarcodeVariant) ? variant.BarcodeVariant : product.Barcode;
        var mrp = variant.EffectivePrice > 0 ? variant.EffectivePrice : product.SellingPrice;

        return 
$@"SIZE 50 mm, 25 mm
GAP 2 mm, 0 mm
DIRECTION 1
CLS
TEXT 15,10,""2"",0,1,1,""{settings.StoreName.ToUpperInvariant()}""
TEXT 15,35,""1"",0,1,1,""{product.Name}""
TEXT 15,55,""1"",0,1,1,""SIZE: {variant.Size} | {variant.Color}""
TEXT 15,75,""2"",0,1,1,""MRP: {settings.CurrencySymbol} {mrp:F2}""
BARCODE 15,100,""128"",40,1,0,2,2,""{barcode}""
PRINT {copies},1
";
    }

    public string GenerateHtmlLabelPreview(Product product, ProductVariant variant, StoreSettings settings, int copies = 1)
    {
        var barcode = !string.IsNullOrWhiteSpace(variant.BarcodeVariant) ? variant.BarcodeVariant : product.Barcode;
        var mrp = variant.EffectivePrice > 0 ? variant.EffectivePrice : product.SellingPrice;

        string barcodeBase64 = "";
        try
        {
            using var bmp = GenerateBarcodeBitmap(barcode, 300, 75);
            using var ms = new MemoryStream();
            bmp.Save(ms, ImageFormat.Png);
            barcodeBase64 = Convert.ToBase64String(ms.ToArray());
        }
        catch
        {
            // Fallback without image
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang='en'>");
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset='utf-8'/>");
        sb.AppendLine($"<title>Label Preview - {System.Net.WebUtility.HtmlEncode(product.Name)}</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("@page { size: 50mm 25mm; margin: 0; }");
        sb.AppendLine("@media print { .no-print { display: none !important; } body { padding: 0 !important; background: transparent !important; } }");
        sb.AppendLine("body { margin: 0; padding: 16px; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; background: #0F172A; color: #F8FAFC; }");
        sb.AppendLine(".toolbar { background: #1E293B; border: 1px solid #334155; padding: 14px 20px; border-radius: 8px; margin-bottom: 24px; display: flex; align-items: center; justify-content: space-between; max-width: 760px; margin-left: auto; margin-right: auto; box-shadow: 0 4px 12px rgba(0,0,0,0.4); }");
        sb.AppendLine(".btn-print { background: #10B981; color: white; border: none; padding: 10px 22px; font-size: 13.5px; font-weight: bold; border-radius: 6px; cursor: pointer; transition: 0.2s; display: inline-flex; align-items: center; gap: 6px; }");
        sb.AppendLine(".btn-print:hover { background: #059669; }");
        sb.AppendLine(".container { display: flex; flex-wrap: wrap; gap: 14px; justify-content: center; max-width: 800px; margin: 0 auto; }");
        sb.AppendLine(".label-card { width: 50mm; height: 25mm; background: #FFFFFF; color: #0F172A; border: 1px dashed #94A3B8; border-radius: 4px; box-sizing: border-box; padding: 2.5mm 2mm; display: flex; flex-direction: column; align-items: center; justify-content: space-between; page-break-inside: avoid; page-break-after: always; box-shadow: 0 4px 6px -1px rgba(0,0,0,0.3); }");
        sb.AppendLine(".store-name { font-size: 7.5pt; font-weight: 800; text-transform: uppercase; letter-spacing: 0.5px; text-align: center; }");
        sb.AppendLine(".prod-name { font-size: 7pt; font-weight: 600; text-align: center; color: #334155; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; max-width: 46mm; }");
        sb.AppendLine(".spec-row { font-size: 6.5pt; font-weight: 700; color: #1E293B; text-align: center; }");
        sb.AppendLine(".price-row { font-size: 7.5pt; font-weight: 800; color: #000; text-align: center; }");
        sb.AppendLine(".barcode-img { width: 44mm; height: 7.5mm; object-fit: fill; }");
        sb.AppendLine(".barcode-txt { font-family: monospace; font-size: 6.5pt; color: #475569; letter-spacing: 1px; text-align: center; }");
        sb.AppendLine("</style>");
        sb.AppendLine("<script>");
        sb.AppendLine("// Auto-trigger print modal in browser (Edge / Chrome)");
        sb.AppendLine("window.onload = function() { setTimeout(function() { window.print(); }, 400); };");
        sb.AppendLine("</script>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");

        sb.AppendLine("<div class='toolbar no-print'>");
        sb.AppendLine($"  <div><span style='font-size: 15px; font-weight: 700; color: #60A5FA;'>🏷️ {System.Net.WebUtility.HtmlEncode(product.Name)}</span> <span style='color: #94A3B8; font-size: 13px;'>({variant.Size} - {copies} Copies)</span></div>");
        sb.AppendLine("  <button class='btn-print' onclick='window.print()'>🖨️ Print Labels / Save PDF</button>");
        sb.AppendLine("</div>");

        sb.AppendLine("<div class='container'>");
        for (int i = 0; i < copies; i++)
        {
            sb.AppendLine("  <div class='label-card'>");
            sb.AppendLine($"    <div class='store-name'>{System.Net.WebUtility.HtmlEncode(settings.StoreName)}</div>");
            sb.AppendLine($"    <div class='prod-name'>{System.Net.WebUtility.HtmlEncode(product.Name)}</div>");
            sb.AppendLine($"    <div class='spec-row'>SZ: {System.Net.WebUtility.HtmlEncode(variant.Size)} | {System.Net.WebUtility.HtmlEncode(variant.Color)}</div>");
            sb.AppendLine($"    <div class='price-row'>MRP: {settings.CurrencySymbol}{mrp:N2} (Incl. Taxes)</div>");
            if (!string.IsNullOrEmpty(barcodeBase64))
            {
                sb.AppendLine($"    <img class='barcode-img' src='data:image/png;base64,{barcodeBase64}' alt='Barcode'/>");
            }
            sb.AppendLine($"    <div class='barcode-txt'>{System.Net.WebUtility.HtmlEncode(barcode)}</div>");
            sb.AppendLine("  </div>");
        }
        sb.AppendLine("</div>");

        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    public bool PrintLabel(Product product, ProductVariant variant, StoreSettings settings, int copies = 1, string? printerName = null, bool forceDialog = false)
    {
        var doc = new PrintDocument();
        
        bool isRealPrinter = !string.IsNullOrWhiteSpace(printerName) && 
                             !printerName.StartsWith("None", StringComparison.OrdinalIgnoreCase) &&
                             !printerName.Contains("PDF Preview", StringComparison.OrdinalIgnoreCase);

        if (isRealPrinter)
        {
            doc.PrinterSettings.PrinterName = printerName!;
        }

        doc.PrinterSettings.Copies = (short)Math.Max(1, copies);

        // Standard 50mm x 25mm label (1.97" x 0.98" in 100ths of an inch: ~197 x 98)
        doc.DefaultPageSettings.PaperSize = new PaperSize("ThermalLabel", 197, 98);
        doc.DefaultPageSettings.Margins = new Margins(5, 5, 5, 5);

        var barcode = !string.IsNullOrWhiteSpace(variant.BarcodeVariant) ? variant.BarcodeVariant : product.Barcode;
        var mrp = variant.EffectivePrice > 0 ? variant.EffectivePrice : product.SellingPrice;

        int printedCopies = 0;

        doc.PrintPage += (sender, e) =>
        {
            var g = e.Graphics;
            if (g == null) return;

            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

            using var storeFont = new Font("Arial", 8.5f, System.Drawing.FontStyle.Bold);
            using var titleFont = new Font("Arial", 7.5f, System.Drawing.FontStyle.Regular);
            using var specFont = new Font("Arial", 7f, System.Drawing.FontStyle.Bold);
            using var priceFont = new Font("Arial", 9f, System.Drawing.FontStyle.Bold);
            using var textBrush = new SolidBrush(System.Drawing.Color.Black);

            // Store Name Header
            var storeText = settings.StoreName.Length > 24 ? settings.StoreName[..24] : settings.StoreName;
            g.DrawString(storeText.ToUpperInvariant(), storeFont, textBrush, 6, 4);

            // Product Name
            var nameText = product.Name.Length > 28 ? product.Name[..28] : product.Name;
            g.DrawString(nameText, titleFont, textBrush, 6, 18);

            // Size, Color, Fit spec
            var specText = $"SZ: {variant.Size} | {variant.Color}";
            if (specText.Length > 30) specText = specText[..30];
            g.DrawString(specText, specFont, textBrush, 6, 30);

            // Price / MRP
            var priceText = $"MRP: {settings.CurrencySymbol}{mrp:N2} (Incl. Taxes)";
            g.DrawString(priceText, priceFont, textBrush, 6, 43);

            // Barcode image
            try
            {
                using var barcodeBmp = GenerateBarcodeBitmap(barcode, 180, 32);
                g.DrawImage(barcodeBmp, 8, 58, 180, 32);
            }
            catch
            {
                // Fallback to text
                g.DrawString(barcode, specFont, textBrush, 20, 65);
            }

            printedCopies++;
            e.HasMorePages = printedCopies < copies;
        };

        // If forceDialog requested or printer is invalid / None, always display native Windows Print Dialog!
        if (forceDialog || !isRealPrinter || !doc.PrinterSettings.IsValid)
        {
            var printDlg = new System.Windows.Controls.PrintDialog();
            if (printDlg.ShowDialog() == true)
            {
                if (printDlg.PrintQueue != null && !string.IsNullOrWhiteSpace(printDlg.PrintQueue.FullName))
                {
                    doc.PrinterSettings.PrinterName = printDlg.PrintQueue.FullName;
                }
                doc.Print();
                return true;
            }
            return false;
        }

        doc.Print();
        return true;
    }

    private static BitmapSource ToBitmapSource(Bitmap bitmap)
    {
        using var memory = new MemoryStream();
        bitmap.Save(memory, ImageFormat.Png);
        memory.Position = 0;

        var bitmapImage = new BitmapImage();
        bitmapImage.BeginInit();
        bitmapImage.StreamSource = memory;
        bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
        bitmapImage.EndInit();
        bitmapImage.Freeze();
        return bitmapImage;
    }
}
