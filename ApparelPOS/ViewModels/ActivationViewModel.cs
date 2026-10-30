using System.IO;
using System.Windows;
using ApparelPOS.Services;
using CommunityToolkit.Mvvm.Input;

namespace ApparelPOS.ViewModels;

public class ActivationViewModel : ViewModelBase
{
    private readonly ILicenseService _licenseService;
    private readonly IHardwareInfoService _hardwareInfoService;
    private readonly IBarcodeService _barcodeService;

    public event Action? ActivationSuccessful;

    public string MachineCode { get; }
    public string PaymentUrl { get; }
    public System.Windows.Media.ImageSource? QrCodeImageSource { get; }
    public string HeaderSubtitle { get; }

    private string _licenseInput = string.Empty;
    public string LicenseInput
    {
        get => _licenseInput;
        set => SetProperty(ref _licenseInput, value);
    }

    private string _validationMessage = string.Empty;
    public string ValidationMessage
    {
        get => _validationMessage;
        set => SetProperty(ref _validationMessage, value);
    }

    private bool _hasError;
    public bool HasError
    {
        get => _hasError;
        set => SetProperty(ref _hasError, value);
    }

    public IRelayCommand CopyMachineCodeCommand { get; }
    public IRelayCommand BrowseLicenseFileCommand { get; }
    public IRelayCommand ActivateLicenseCommand { get; }

    public ActivationViewModel(
        ILicenseService licenseService, 
        IHardwareInfoService hardwareInfoService,
        IBarcodeService barcodeService)
    {
        _licenseService = licenseService;
        _hardwareInfoService = hardwareInfoService;
        _barcodeService = barcodeService;

        MachineCode = _hardwareInfoService.GetMachineCode();
        PaymentUrl = $"https://apparelpos-0iti.onrender.com/renew?mc={MachineCode}&amt=249";
        QrCodeImageSource = _barcodeService.GenerateQrCodeImageSource(PaymentUrl, 200);

        var currentLic = _licenseService.GetCachedLicenseInfo();
        if (currentLic.IsClockTampered)
        {
            HeaderSubtitle = "Clock tampering detected. Please correct your system clock.";
            ValidationMessage = currentLic.StatusMessage;
            HasError = true;
        }
        else if (currentLic.IsTrial && currentLic.TrialDaysRemaining <= 0)
        {
            HeaderSubtitle = "Your 90-Day Free Trial has ended. Activate for ₹249/year to continue.";
            ValidationMessage = "Trial period completed. Scan QR on phone to get your 1-year license key.";
            HasError = false;
        }
        else
        {
            HeaderSubtitle = "Instant 1-Year License Activation (₹249/year) • 100% Offline Validation";
        }

        CopyMachineCodeCommand = new RelayCommand(CopyMachineCode);
        BrowseLicenseFileCommand = new RelayCommand(BrowseLicenseFile);
        ActivateLicenseCommand = new RelayCommand(ActivateLicense);
    }

    private void CopyMachineCode()
    {
        try
        {
            Clipboard.SetText(MachineCode);
            ValidationMessage = "Machine code copied to clipboard! Send this code to software vendor.";
            HasError = false;
        }
        catch (Exception ex)
        {
            ValidationMessage = $"Failed to copy: {ex.Message}";
            HasError = true;
        }
    }

    private void BrowseLicenseFile()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "License Files (*.lic)|*.lic|All Files (*.*)|*.*",
            Title = "Select ApparelPOS License File"
        };

        if (dlg.ShowDialog() == true)
        {
            try
            {
                var content = File.ReadAllText(dlg.FileName);
                LicenseInput = content.Trim();
                ActivateLicense();
            }
            catch (Exception ex)
            {
                ValidationMessage = $"Error reading license file: {ex.Message}";
                HasError = true;
            }
        }
    }

    private void ActivateLicense()
    {
        if (string.IsNullOrWhiteSpace(LicenseInput))
        {
            ValidationMessage = "Please paste the license key or browse for a .lic file.";
            HasError = true;
            return;
        }

        var result = _licenseService.ValidateAndSaveLicense(LicenseInput.Trim());
        if (result.IsValid)
        {
            ValidationMessage = $"License activated successfully for {result.CustomerName}! Expiry: {result.ExpiryDate}";
            HasError = false;
            ActivationSuccessful?.Invoke();
        }
        else
        {
            ValidationMessage = result.StatusMessage;
            HasError = true;
        }
    }
}
