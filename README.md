# ApparelPOS - Offline Desktop Point of Sale & Inventory Management System

**ApparelPOS** is a production-grade, 100% offline desktop Point of Sale (POS) and inventory management system purpose-built for clothing, apparel, and fashion retail stores. It operates with zero cloud calls, zero telemetry, and zero web API dependencies, storing all data in a local embedded SQLite database with Write-Ahead Logging (WAL) enabled for crash safety.

The software enforces a hardware-locked (node-locked) cryptographic licensing system using **RSA-2048** digital signatures and Windows WMI immutable hardware identifiers to prevent unauthorized sharing, copying, or piracy.

---

## 1. Architectural Highlights

* **Application Framework:** C# .NET 8 (WPF) with Windows hardware driver and printer integration.
* **Architecture Pattern:** Clean MVVM (Model-View-ViewModel) with `Microsoft.Extensions.DependencyInjection` and `CommunityToolkit.Mvvm`.
* **Local Database Engine:** Embedded SQLite via Entity Framework Core with Write-Ahead Logging (`PRAGMA journal_mode = WAL;`) and memory temp store. Database file resides at `%AppData%/ApparelPOS/apparel_pos.db`.
* **Zero Internet Dependency:** Complete offline operation with air-gapped security, zero cloud telemetry, and offline static UPI QR generation.
* **Hardware Integration:**
  * USB / Wireless HID Handheld Barcode Scanners (Keyboard emulation with automatic `<Enter>` / `<CR>` buffer detection).
  * Direct Thermal Label Printing (50mm x 25mm garment tags with Code 128 barcodes).
  * 2-inch (58mm) and 3-inch (80mm) Thermal Receipt Printers with auto-cut support.
  * Raw TSPL / ESC-POS command generation.

---

## 2. Project Directory Structure

```text
Billing_ERP/
├── ApparelPOS/                      # Main WPF Application (.NET 8 Windows)
│   ├── Data/
│   │   ├── AppDbContext.cs          # SQLite EF Core context with WAL pragmas & indexes
│   │   └── DbInitializer.cs         # Seed data (store settings, categories, apparel variants)
│   ├── Models/
│   │   ├── Category.cs              # Apparel category master (HSN, default tax)
│   │   ├── Product.cs               # Apparel master entity (SKU, MRP, cost, stock)
│   │   ├── ProductVariant.cs        # Matrix variants (Size, Color, Fabric, Fit, Barcode)
│   │   ├── Invoice.cs               # Transaction header (Subtotal, GST, RoundOff, Total)
│   │   ├── InvoiceItem.cs           # Itemized transaction rows with variant references
│   │   ├── StoreSettings.cs         # Store identity, printer mappings, UPI VPA
│   │   └── LicenseInfo.cs           # Cryptographic licensing status
│   ├── Services/
│   │   ├── IHardwareInfoService.cs  # WMI hardware fingerprint interface
│   │   ├── HardwareInfoService.cs   # Motherboard UUID, CPU ID, Disk Serial -> 16-char HWID
│   │   ├── ILicenseService.cs       # RSA-2048 licensing interface
│   │   ├── LicenseService.cs        # Embedded RSA public key signature verification
│   │   ├── IBarcodeService.cs       # ZXing Code 128 and QR code engine
│   │   ├── BarcodeService.cs        # Thermal label printing & TSPL generator
│   │   ├── IPrinterService.cs       # Thermal receipt formatting & printing
│   │   ├── PrinterService.cs        # 58mm/80mm receipt generation & printer dispatch
│   │   ├── IInventoryService.cs     # Catalog & bulk CSV import/export service
│   │   ├── InventoryService.cs      # Product/variant CRUD & offline CSV engine
│   │   ├── IBillingService.cs       # Barcode lookup, atomic checkout transactions
│   │   ├── BillingService.cs        # ACID stock deduction & sequential invoice numbering
│   │   ├── IAnalyticsService.cs     # Local revenue, gross profit, and size metrics
│   │   └── AnalyticsService.cs      # Sales aggregation and reporting
│   ├── ViewModels/
│   │   ├── ViewModelBase.cs         # ObservableObject base with busy/status tracking
│   │   ├── ActivationViewModel.cs   # HWID display, license paste & file activation
│   │   ├── BillingViewModel.cs      # Fast POS cashier workflow & cart calculations
│   │   ├── InventoryViewModel.cs    # Product catalog & apparel variant matrix
│   │   ├── BarcodeGeneratorViewModel.cs # Thermal label designer & previewer
│   │   ├── AnalyticsViewModel.cs    # KPI cards, apparel size insights, receipt reprint
│   │   ├── SettingsViewModel.cs     # Hardware printer configuration & store details
│   │   └── MainViewModel.cs         # Navigation host, status bar, and live ticker
│   ├── Views/
│   │   ├── ActivationWindow.xaml    # Anti-piracy activation window
│   │   ├── MainWindow.xaml          # Main application frame & sidebar
│   │   ├── BillingView.xaml         # Rapid POS checkout interface
│   │   ├── InventoryView.xaml       # Catalog management & matrix variants
│   │   ├── BarcodeGeneratorView.xaml# Thermal label preview & TSPL commands
│   │   ├── AnalyticsView.xaml       # Local reporting & sales history
│   │   └── SettingsView.xaml        # System & printer configuration
│   ├── Styles/
│   │   ├── Colors.xaml              # Curated dark slate & indigo palette
│   │   └── AppStyles.xaml           # Glassmorphism cards, buttons, datagrids
│   ├── App.xaml                     # Resource dictionary merging
│   └── App.xaml.cs                  # DI container bootstrap & license startup check
├── ApparelPOS.Tests/                # Automated xUnit test suite (7 tests)
│   └── ApparelPosTests.cs           # HWID, RSA signing, tamper rejection, POS checkout
├── generate_license.py              # Seller's offline RSA-2048 license generator
├── private_key.pem                  # Seller's RSA private key (keep secret)
├── public_key.pem                   # Public key embedded in C# code
├── license.lic                      # Generated machine-locked license file
└── sample_apparel_inventory.csv     # Bulk apparel CSV template
```

---

## 3. Hardware-Locked Cryptographic Licensing (Anti-Piracy)

### Machine Fingerprint Generation (HWID)
The application computes an immutable hardware fingerprint by querying Windows WMI via `HardwareInfoService.cs`:
1. **Motherboard UUID:** `Win32_ComputerSystemProduct.UUID` (fallback to `Win32_BaseBoard.SerialNumber`).
2. **CPU Processor ID:** `Win32_Processor.ProcessorId` (fallback to `UniqueId`).
3. **Primary Disk Serial:** `Win32_DiskDrive.SerialNumber` (fallback to `Win32_PhysicalMedia.SerialNumber`).
4. **Registry MachineGuid:** `HKLM\SOFTWARE\Microsoft\Cryptography\MachineGuid`.

These immutable hardware identifiers are salted, hashed with **SHA-256**, and truncated into a clean 16-character alphanumeric string formatted in 4 blocks of 4 (e.g. `E39A-A2EC-B814-9399`).

### Cryptographic Validation (RSA-2048)
* The C# application embeds an RSA-2048 Public Key (`LicenseService.cs`).
* The software vendor uses the standalone `generate_license.py` script containing the matching private key to issue licenses.
* License format: `base64(JSON_PAYLOAD).base64(RSA_SHA256_SIGNATURE)`.
* On startup, `App.xaml.cs` loads `%AppData%/ApparelPOS/license.lic`. If valid and the machine code matches the current computer's HWID, the POS loads. Otherwise, it presents the `ActivationWindow`.

### Using the Seller's License Generator Tool
```bash
# Initialize keypair (if not present):
python generate_license.py --init-keys

# Issue a lifetime license locked to a customer's machine code:
python generate_license.py -m "E39A-A2EC-B814-9399" -c "Swapnil Pawar" -s "Velvet Thread Flagship Store" -e "LIFETIME" -o "license.lic"

# Issue an annual license:
python generate_license.py -m "E39A-A2EC-B814-9399" -c "Retail Client" -s "Branch Store #2" -e "2027-12-31" -o "license.lic"

# Verify a license string or file:
python generate_license.py --verify license.lic
```

---

## 4. Apparel Matrix Inventory & Catalog

Apparel retail requires a 2-tier matrix model:
1. **Master Product:** Product Name, Category, Base SKU, Cost Price, Selling Price (MRP), Tax Rate (GST %), Low Stock Threshold.
2. **Matrix Variant:** Size (S, M, L, XL, 38, 40, 42), Color (Navy, White, Olive), Fabric (Cotton, Linen, Denim), Fit Type (Slim, Regular, Relaxed), Scannable Barcode, Stock Quantity.

### Bulk CSV Import/Export
The application includes a built-in CSV parser and exporter.
* **Export:** Generates `ApparelInventory_YYYYMMDD.csv` with full variant matrix.
* **Import:** Bulk upserts products and variants offline with error reporting.
* A sample template `sample_apparel_inventory.csv` is included in the project root.

---

## 5. Fast POS Billing & Cashier Workflows

### Keyboard Shortcuts
* **`[F1]`**: Start New Bill (clears cart, resets totals).
* **`[F2]`**: Focus Barcode Scanner input box.
* **`[F5]`**: Focus Bill Discount field.
* **`[F8]`**: Toggle Full-Screen UPI QR Code modal.
* **`[F9]`**: Checkout and Print Thermal Receipt.
* **`[Esc]`**: Dismiss active modal / clear.

### Barcode Hardware Scanning (HID Keyboard Emulation)
Standard USB and wireless handheld scanners operate in HID keyboard emulation mode. `BillingView.xaml.cs` and `MainWindow.xaml.cs` monitor scan input and `<Enter>` suffixes, immediately looking up the variant in the local database and appending it to the cart without manual mouse interaction.

### Live Calculations & Tax Breakdown
* Line-item and bill-level flat or percentage discounts.
* Indian GST breakdown (CGST 2.5% + SGST 2.5% for standard apparel, or custom tax rate).
* Live round-off calculation to the nearest rupee.
* Live cash change calculator (`Change Due = Cash Received - Grand Total`).

### Offline Static UPI QR Code Generation
Generates NPCI standard UPI QR codes offline via `ZXing.Net` using merchant VPA:
`upi://pay?pa=merchant@upi&pn=StoreName&am=TOTAL_AMT&tn=Bill_INV-XXXX&cu=INR`
Customers can scan directly off the cashier screen or from the thermal receipt.

---

## 6. Thermal Label & Receipt Printing

### Thermal Garment Hangtag (50mm x 25mm / 60mm x 40mm)
* Uses `System.Drawing.Printing.PrintDocument` with custom page sizing (197 x 98 hundredths of an inch).
* Prints Store Name, Apparel Item Name, Size, Color, Fit, MRP, and high-resolution Code 128 barcode.
* Generates TSPL raw commands for direct socket/serial printing on TSC, Xprinter, TVS, and Zebra thermal printers.

### Thermal Receipt Printing (58mm / 80mm)
* Formats itemized receipt with store branding, GSTIN, invoice metadata, apparel size/color details, tax breakdown, and return/exchange policy footer.
* Silent print to configured Windows printer driver.
* Safe fallback: If the printer is offline or missing paper, the application preserves the checkout and presents an on-screen receipt preview dialog with 1-click retry.

---

## 7. Sales Analytics & Profit/Loss Estimations

* **Local Sales Summary:** Total Revenue, Total Invoices, Items Sold, Gross Profit (`Revenue - Cost of Goods Sold`), and Margin %.
* **Payment Split:** Cash vs Card vs UPI breakdown.
* **Apparel Matrix Insights:** Fastest-moving garment sizes (e.g. Size 40 vs 42 popularity) and top categories.
* **Sales History & Reprint:** Date-filtered sales history with 1-click thermal receipt reprinting.

---

## 8. Database Crash Safety & Recovery Strategies

1. **SQLite WAL (Write-Ahead Logging):**
   * Configured via `PRAGMA journal_mode = WAL;` and `PRAGMA synchronous = NORMAL;`.
   * Ensures readers do not block writers and transactions are crash-resilient against sudden power loss.
2. **Atomic Checkout Transactions:**
   * All inventory stock reductions and invoice writes run inside a single ACID database transaction (`BeginTransactionAsync`).
3. **Database Corruption Prevention:**
   * SQLite busy timeout is configured to 5000ms with memory temp stores to avoid lock contention.
4. **Printer Fail-Safe Recovery:**
   * Printing exceptions do not abort or roll back financial transactions; the invoice is saved and the cashier is notified with an on-screen preview.

---

## 9. Running and Testing

### Build the Application
```bash
cd ApparelPOS
dotnet build
```

### Run All Automated Tests
```bash
cd ApparelPOS.Tests
dotnet test
```

### Run the Desktop POS Application
```bash
cd ApparelPOS
dotnet run
```
