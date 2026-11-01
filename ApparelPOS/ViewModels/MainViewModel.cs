using System.Windows.Threading;
using ApparelPOS.Models;
using ApparelPOS.Services;
using CommunityToolkit.Mvvm.Input;

namespace ApparelPOS.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly ILicenseService _licenseService;
    private readonly IBillingService _billingService;

    public BillingViewModel BillingVm { get; }
    public InventoryViewModel InventoryVm { get; }
    public BarcodeGeneratorViewModel BarcodeVm { get; }
    public AnalyticsViewModel AnalyticsVm { get; }
    public SettingsViewModel SettingsVm { get; }

    private ViewModelBase _currentView;
    public ViewModelBase CurrentView
    {
        get => _currentView;
        set
        {
            if (SetProperty(ref _currentView, value))
            {
                OnPropertyChanged(nameof(IsBillingActive));
                OnPropertyChanged(nameof(IsInventoryActive));
                OnPropertyChanged(nameof(IsBarcodeActive));
                OnPropertyChanged(nameof(IsAnalyticsActive));
                OnPropertyChanged(nameof(IsSettingsActive));
            }
        }
    }

    public bool IsBillingActive => CurrentView == BillingVm;
    public bool IsInventoryActive => CurrentView == InventoryVm;
    public bool IsBarcodeActive => CurrentView == BarcodeVm;
    public bool IsAnalyticsActive => CurrentView == AnalyticsVm;
    public bool IsSettingsActive => CurrentView == SettingsVm;

    private string _storeName = "Velvet Thread Apparel Co.";
    public string StoreName
    {
        get => _storeName;
        set => SetProperty(ref _storeName, value);
    }

    private string _licenseBadge = "COMMERCIAL";
    public string LicenseBadge
    {
        get => _licenseBadge;
        set => SetProperty(ref _licenseBadge, value);
    }

    private bool _isTrialActive;
    public bool IsTrialActive
    {
        get => _isTrialActive;
        set => SetProperty(ref _isTrialActive, value);
    }

    private string _currentTime = DateTime.Now.ToString("dd-MMM-yyyy | HH:mm:ss");
    public string CurrentTime
    {
        get => _currentTime;
        set => SetProperty(ref _currentTime, value);
    }

    public string CashierName => "Admin Cashier";

    // Navigation Commands
    public IRelayCommand NavigateToBillingCommand { get; }
    public IRelayCommand NavigateToInventoryCommand { get; }
    public IRelayCommand NavigateToBarcodeCommand { get; }
    public IRelayCommand NavigateToAnalyticsCommand { get; }
    public IRelayCommand NavigateToSettingsCommand { get; }
    public IRelayCommand OpenActivationCommand { get; }

    private readonly IServiceProvider _serviceProvider;

    public MainViewModel(
        ILicenseService licenseService,
        IBillingService billingService,
        IServiceProvider serviceProvider,
        BillingViewModel billingVm,
        InventoryViewModel inventoryVm,
        BarcodeGeneratorViewModel barcodeVm,
        AnalyticsViewModel analyticsVm,
        SettingsViewModel settingsVm)
    {
        _licenseService = licenseService;
        _billingService = billingService;
        _serviceProvider = serviceProvider;

        BillingVm = billingVm;
        InventoryVm = inventoryVm;
        BarcodeVm = barcodeVm;
        AnalyticsVm = analyticsVm;
        SettingsVm = settingsVm;

        _currentView = billingVm;

        NavigateToBillingCommand = new RelayCommand(() =>
        {
            CurrentView = BillingVm;
            _ = BillingVm.LoadStoreSettingsAsync();
        });
        NavigateToInventoryCommand = new RelayCommand(() => { CurrentView = InventoryVm; _ = InventoryVm.LoadProductsAsync(); });
        NavigateToBarcodeCommand = new RelayCommand(() => CurrentView = BarcodeVm);
        NavigateToAnalyticsCommand = new RelayCommand(() => { CurrentView = AnalyticsVm; _ = AnalyticsVm.LoadAnalyticsAsync(); });
        NavigateToSettingsCommand = new RelayCommand(() => CurrentView = SettingsVm);
        OpenActivationCommand = new RelayCommand(OpenActivation);

        // Timer for clock in top bar
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += (s, e) => CurrentTime = DateTime.Now.ToString("dd-MMM-yyyy | HH:mm:ss");
        timer.Start();

        _ = InitializeAsync();
    }

    private void OpenActivation()
    {
        try
        {
            var activationWin = (Views.ActivationWindow)_serviceProvider.GetService(typeof(Views.ActivationWindow))!;
            if (activationWin.ShowDialog() == true)
            {
                RefreshLicenseStatus();
            }
        }
        catch { }
    }

    private void RefreshLicenseStatus()
    {
        var lic = _licenseService.GetCachedLicenseInfo();
        if (lic.IsTrial)
        {
            IsTrialActive = true;
            LicenseBadge = $"🎉 90-DAY TRIAL ({lic.TrialDaysRemaining} Days Left)";
        }
        else if (lic.IsValid)
        {
            IsTrialActive = false;
            LicenseBadge = $"PRO: COMMERCIAL ({lic.ExpiryDate})";
        }
        else
        {
            IsTrialActive = false;
            LicenseBadge = "UNLICENSED";
        }
    }

    private async Task InitializeAsync()
    {
        var settings = await _billingService.GetStoreSettingsAsync();
        StoreName = settings.StoreName;
        RefreshLicenseStatus();
    }
}
