using System.Drawing;
using System.Windows.Media.Imaging;
using ApparelPOS.Models;

namespace ApparelPOS.Services;

public interface IBarcodeService
{
    Bitmap GenerateBarcodeBitmap(string content, int width = 320, int height = 100);
    BitmapSource? GenerateBarcodeImageSource(string content, int width = 320, int height = 100);
    BitmapSource? GenerateQrCodeImageSource(string content, int size = 240);
    string GenerateTsplCommands(Product product, ProductVariant variant, StoreSettings settings, int copies = 1);
    string GenerateHtmlLabelPreview(Product product, ProductVariant variant, StoreSettings settings, int copies = 1);
    bool PrintLabel(Product product, ProductVariant variant, StoreSettings settings, int copies = 1, string? printerName = null, bool forceDialog = false);
}
