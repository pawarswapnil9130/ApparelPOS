using ApparelPOS.Models;

namespace ApparelPOS.Services;

public interface IPrinterService
{
    List<string> GetInstalledPrinters();
    string GetDefaultPrinterName();
    string GenerateReceiptText(Invoice invoice, StoreSettings settings, int charWidth = 42);
    void PrintReceipt(Invoice invoice, StoreSettings settings, string? targetPrinterName = null);
}
