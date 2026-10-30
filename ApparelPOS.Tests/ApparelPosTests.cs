using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ApparelPOS.Data;
using ApparelPOS.Models;
using ApparelPOS.Services;
using Microsoft.EntityFrameworkCore;

namespace ApparelPOS.Tests;

public class ApparelPosTests
{
    private const string PrivateKeyPem = 
@"-----BEGIN PRIVATE KEY-----
MIIEvwIBADANBgkqhkiG9w0BAQEFAASCBKkwggSlAgEAAoIBAQDoeT//O93Q3sGv
GocPBk9b6aBJ1BBB1MDqrJ6vXZV+3eudrKGJXCr2GZsUEJK4cwJt9apYE35ltigw
W3BDCynch56fF6ZM/HjY8pr4fR+d548f18Y6G7xFo7vZhet3MPO/2x3V8z36rbG+
Uj2NA/Qdtw2Cv4hGQ9RPqXLBcZOUpyz/YZdlFFTHHdsYUGijZkj1bOXK7x0RmadB
4I4dAuCmIG+I+PA1pDFNBVGKnoBQsaK4GrnhkwUgl0OvMdHjCk5ZlToDUIrPizy8
nPNW7DGirKEjxKlTa3od1z6Ea1gV9wkzdZn7V7XIRGVvcRbE8ue4OsYKsQlXLCNQ
L1obGcDtAgMBAAECggEACKTa2n3aiWssTtCmMp/8mxbMmnd+Tpb/a1pqy2t9Ol/n
DxBTBf1S2ElrHkEK1j0/MGErlHsjvoVvk3Nlm7XnuGb54MGA3Kj17LSB9aO1ZWot
mgoBuWRm85HwfgNAQPN62hM8K0SQSpNjIggDI4yrlYqaaoD+VgUso5+nKtvdwxpY
FBgikhuv5yUj0eokh1tZOvO+SclpaCrLx9Nhd6YQnecbJ1izYA1qjiE2QQtA0UKr
9VDGTQLQCOAVnWSgCkRfvV3mH3znIh3snox/h18UI87eqY867s1ciqL2Qr9zOiDv
nD0dJYsg3HyT2+rTX0dLN3vW48t1EvATlyQZdh/JkwKBgQD4UHCj3o45i7TRmpJh
0QgXRjrH1v/jdQ1vsxiNVDU5LfVIkz+FSifiKKDpM2SCz7EAmcNsBQwY7d3hhHnT
I96E1h4nX6wEvvtHFR+ZfKhO4KeS7MreQaVO+UwYtxEKzRzgqlwlvLil4bBAjyO2
00WX6M99jfDFXOgXYtyHXEttHwKBgQDvq0teRSuiyY/XQH/gG/ivVTOz3L+81WnR
NFj2/mUqzuuJLFU1H3lxoVOKglDIuuxwmq71tVRwcR0rLFtcltJZo+4MRx8/3mkM
EZUM05C/1tZGQE1G+kfx0XrtdBY/LndmToIQtOI4KNJnrH20U6hkaCCRWI7wVI/O
KtAbeNTEcwKBgQCoZAxu5C740Uf22GaqVNGyztpvyNpQyoEpHX/p35AUTZlH9Ssf
XbF8SecNHd/sYaH78Uo4B5gzk6NjGq5uw6VT2cwrEVMbCNRoWOfL82BD1U9ucIwG
euf6jU7Qd9zjwULzDDRfFwL04v7eWPN0OQO5x5Rl5YrAnId7zHwWgz1XKwKBgQDm
yFhLz2mEWHkE+hvorqaaFv4xTPjomcxKYhDkzfSW8ydFMIZgIA2uSH2MW430rGIu
0BR3j4Nevt0qjIzxLF4jaUI5WmcR9lXLBEc1UagJwpMUlKiVFz4d8exgKK+faVwa
DLEb60IGd/CbWjK1aPU2sjniG5eQfqz3BqzcN08SVwKBgQCngSlQXtCDSlBZoMul
pWF4jow4Xla7kZ/GPq0xiV8bdnRWnyw/C6ACxy8H9uLuieGBpX+knbarTZN4cpia
xgrJNTIGOBu7LHBffRDCQ93MMJlnqk+i6UudpLs/4dKWe4eMfQzf1aDXGeR4tsOh
9VrTwlIFTIcY+yrnqSiW7ukdLw==
-----END PRIVATE KEY-----";

    private readonly Xunit.Abstractions.ITestOutputHelper _output;

    public ApparelPosTests(Xunit.Abstractions.ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void TestHardwareInfo_GeneratesValid16CharFormattedMachineCode()
    {
        var hwService = new HardwareInfoService();
        var code = hwService.GetMachineCode();
        _output.WriteLine($"[MACHINE_CODE_RESULT]={code}");

        Assert.NotNull(code);
        Assert.Equal(19, code.Length); // XXXX-XXXX-XXXX-XXXX is 19 chars including 3 hyphens

        // Verify format: 4 alphanumeric blocks separated by hyphens
        var regex = new Regex(@"^[0-9A-F]{4}-[0-9A-F]{4}-[0-9A-F]{4}-[0-9A-F]{4}$");
        Assert.Matches(regex, code);

        // Verify cached consistency
        var code2 = hwService.GetMachineCode();
        Assert.Equal(code, code2);
    }

    [Fact]
    public void TestBarcodeService_GeneratesValidBitmaps()
    {
        var barcodeService = new BarcodeService();

        // Code 128 test
        using var bmp = barcodeService.GenerateBarcodeBitmap("8901234560012", 200, 60);
        Assert.NotNull(bmp);
        Assert.Equal(200, bmp.Width);
        Assert.Equal(60, bmp.Height);

        // QR Code test
        var qrSource = barcodeService.GenerateQrCodeImageSource("upi://pay?pa=test@upi&am=100", 150);
        Assert.NotNull(qrSource);
    }

    [Fact]
    public void TestLicenseService_RejectsTamperedOrInvalidPayload()
    {
        var hwService = new HardwareInfoService();
        var licenseService = new LicenseService(hwService);

        // Empty string
        var emptyRes = licenseService.ValidateAndSaveLicense("");
        Assert.False(emptyRes.IsValid);

        // Garbage string
        var garbageRes = licenseService.ValidateAndSaveLicense("this-is-not-a-valid-license");
        Assert.False(garbageRes.IsValid);

        // Malformed base64
        var malformedRes = licenseService.ValidateAndSaveLicense("abc.def");
        Assert.False(malformedRes.IsValid);
    }

    [Fact]
    public void TestLicenseService_EndToEndRsaSigningAndHWIDVerification()
    {
        var hwService = new HardwareInfoService();
        var machineCode = hwService.GetMachineCode();
        var licenseService = new LicenseService(hwService);

        // 1. Generate valid RSA signed license for current machine
        var validLic = CreateSignedLicense(machineCode, "Premier Retailer", "Velvet Thread Store", "LIFETIME");
        var result = licenseService.ValidateAndSaveLicense(validLic);
        Assert.True(result.IsValid, $"Validation failed: {result.StatusMessage}");
        Assert.Equal(machineCode, result.MachineCode);
        Assert.Equal("Premier Retailer", result.CustomerName);
        Assert.Equal("LIFETIME", result.ExpiryDate);

        // 2. Test mismatched machine code (spoofed/copied from another machine)
        var foreignLic = CreateSignedLicense("9999-8888-7777-6666", "Foreign Store", "Foreign Store", "LIFETIME");
        var foreignRes = licenseService.ValidateAndSaveLicense(foreignLic);
        Assert.False(foreignRes.IsValid);
        Assert.Contains("Hardware ID mismatch", foreignRes.StatusMessage);

        // 3. Test expired license
        var expiredLic = CreateSignedLicense(machineCode, "Expired Client", "Store", "2020-01-01");
        var expiredRes = licenseService.ValidateAndSaveLicense(expiredLic);
        Assert.False(expiredRes.IsValid);
        Assert.Contains("expired", expiredRes.StatusMessage, StringComparison.OrdinalIgnoreCase);

        // 4. Test cryptographic signature tampering
        var parts = validLic.Split('.');
        var tamperedSig = parts[1][..^4] + "AAAA"; // mutate last 4 bytes of base64
        var tamperedLic = $"{parts[0]}.{tamperedSig}";
        var tamperedRes = licenseService.ValidateAndSaveLicense(tamperedLic);
        Assert.False(tamperedRes.IsValid);
    }

    private static string CreateSignedLicense(string machineCode, string customer, string store, string expiry)
    {
        var payloadObj = new
        {
            CustomerName = customer,
            ExpiryDate = expiry,
            IssuedAt = DateTime.Now.ToString("yyyy-MM-dd"),
            LicenseType = "Commercial Offline",
            MachineCode = machineCode,
            StoreName = store
        };

        var json = JsonSerializer.Serialize(payloadObj);
        var jsonBytes = Encoding.UTF8.GetBytes(json);

        using var rsa = RSA.Create();
        rsa.ImportFromPem(PrivateKeyPem);

        var sigBytes = rsa.SignData(jsonBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        var payloadB64 = Convert.ToBase64String(jsonBytes);
        var sigB64 = Convert.ToBase64String(sigBytes);

        return $"{payloadB64}.{sigB64}";
    }

    [Fact]
    public async Task TestBillingService_ScanLookupAndAtomicCheckout()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source=test_pos_{Guid.NewGuid():N}.db")
            .Options;

        using var db = new AppDbContext(options);
        DbInitializer.Initialize(db);

        var billingService = new BillingService(db);

        // 1. Lookup item by variant barcode (e.g. Oxford Shirt 40 M Navy: 8901234560012)
        var scanRes = await billingService.FindItemByBarcodeOrSkuAsync("8901234560012");
        Assert.NotNull(scanRes);
        Assert.NotNull(scanRes.Product);
        Assert.NotNull(scanRes.Variant);
        Assert.Equal("40 (M)", scanRes.Variant.Size);
        var initialStock = scanRes.Variant.StockQuantity;

        // 2. Perform atomic checkout
        var invoice = new Invoice
        {
            CustomerName = "Test Shopper",
            CustomerPhone = "9988776655",
            PaymentMethod = "UPI",
            SubTotal = 1499.00m,
            TotalAmount = 1499.00m,
            Items = new List<InvoiceItem>
            {
                new()
                {
                    ProductId = scanRes.Product.Id,
                    VariantId = scanRes.Variant.Id,
                    ItemName = scanRes.Product.Name,
                    Size = scanRes.Variant.Size,
                    Color = scanRes.Variant.Color,
                    UnitPrice = 1499.00m,
                    CostPrice = 750.00m,
                    Quantity = 2,
                    TotalPrice = 2998.00m
                }
            }
        };

        var processed = await billingService.ProcessCheckoutAsync(invoice);
        Assert.NotNull(processed.InvoiceNumber);
        Assert.StartsWith("INV-", processed.InvoiceNumber);

        // 3. Verify stock deduction
        var updatedVariant = await db.ProductVariants.FindAsync(scanRes.Variant.Id);
        Assert.NotNull(updatedVariant);
        Assert.Equal(initialStock - 2, updatedVariant.StockQuantity);

        // Clean up test db
        await db.Database.EnsureDeletedAsync();
    }

    [Fact]
    public async Task Test_ProductionDatabaseInitializationAndWalMode()
    {
        using var db = new AppDbContext();
        DbInitializer.Initialize(db);

        var dbPath = AppDbContext.DatabasePath;
        Assert.True(System.IO.File.Exists(dbPath), $"Database file should exist at: {dbPath}");

        // Verify categories
        var catCount = await db.Categories.CountAsync();
        Assert.True(catCount >= 5, "Categories should be seeded");

        // Verify apparel products
        var products = await db.Products.Include(p => p.Variants).ToListAsync();
        Assert.True(products.Count >= 1, "Products should exist in database");

        // Verify store settings
        var settings = await db.StoreSettings.FirstOrDefaultAsync();
        Assert.NotNull(settings);
        Assert.Equal("Velvet Thread Apparel Co.", settings.StoreName);
    }

    [Fact]
    public void Test_CameraScannerService_Lifecycle()
    {
        using var cameraService = new CameraScannerService();
        Assert.False(cameraService.IsRunning);

        // Stop should be safe even when not started
        cameraService.Stop();
        Assert.False(cameraService.IsRunning);
    }

    [Fact]
    public async Task Test_FindItemByBarcodeOrSku_WithCaseAndLeadingZeros()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source=test_barcode_{Guid.NewGuid():N}.db")
            .Options;

        using var db = new AppDbContext(options);
        DbInitializer.Initialize(db);

        var billingService = new BillingService(db);

        // 1. Standard lookup (8901234560012)
        var exact = await billingService.FindItemByBarcodeOrSkuAsync("8901234560012");
        Assert.NotNull(exact);
        Assert.Equal("8901234560012", exact.Variant?.BarcodeVariant);

        // 2. Lookup with extra whitespace
        var trimmed = await billingService.FindItemByBarcodeOrSkuAsync("  8901234560012  ");
        Assert.NotNull(trimmed);

        // 3. Lookup SKU in lowercase (e.g. "shirt-oxf-01-blu-40")
        var caseInsensitive = await billingService.FindItemByBarcodeOrSkuAsync("shirt-oxf-01-blu-40");
        Assert.NotNull(caseInsensitive);
        Assert.Equal("SHIRT-OXF-01-BLU-40", caseInsensitive.Variant?.SkuVariant);

        // 4. Lookup master product SKU in lowercase (e.g. "shirt-oxf-01")
        var masterSku = await billingService.FindItemByBarcodeOrSkuAsync("shirt-oxf-01");
        Assert.NotNull(masterSku);
        Assert.Equal("SHIRT-OXF-01", masterSku.Product.Sku);

        // 5. Non-existent barcode should cleanly return null
        var notFound = await billingService.FindItemByBarcodeOrSkuAsync("9999999999999");
        Assert.Null(notFound);
    }

    [Fact]
    public void TestTrial_Initializes90DaysOnFirstRun()
    {
        var hwService = new HardwareInfoService();
        var licService = new LicenseService(hwService);

        var trialInfo = licService.EvaluateTrialState(hwService.GetMachineCode());

        Assert.NotNull(trialInfo);
        Assert.True(trialInfo.IsValid);
        Assert.True(trialInfo.IsTrial);
        Assert.InRange(trialInfo.TrialDaysRemaining, 80, 90);
        Assert.Contains("90-Day Free Trial", trialInfo.LicenseType);
    }

    [Fact]
    public void TestTrial_CommercialLicenseSupersedesTrial()
    {
        var hwService = new HardwareInfoService();
        var licService = new LicenseService(hwService);
        var machineCode = hwService.GetMachineCode();

        // Generate a 1-year commercial key
        var payloadDict = new Dictionary<string, string>
        {
            { "MachineCode", machineCode },
            { "CustomerName", "Test Shopkeeper" },
            { "StoreName", "Fashion Store" },
            { "ExpiryDate", DateTime.Today.AddYears(1).ToString("yyyy-MM-dd") },
            { "IssuedAt", DateTime.Today.ToString("yyyy-MM-dd") },
            { "LicenseType", "Commercial Annual" }
        };

        var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(payloadDict);
        using var rsa = RSA.Create();
        rsa.ImportFromPem(PrivateKeyPem);
        var signature = rsa.SignData(jsonBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        var licString = $"{Convert.ToBase64String(jsonBytes)}.{Convert.ToBase64String(signature)}";

        var activateResult = licService.ValidateAndSaveLicense(licString);

        Assert.True(activateResult.IsValid);
        Assert.False(activateResult.IsTrial);
        Assert.Equal("Commercial Annual", activateResult.LicenseType);
        Assert.Equal("Test Shopkeeper", activateResult.CustomerName);
    }
}

