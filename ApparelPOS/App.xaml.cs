using System.Windows;
using ApparelPOS.Data;
using ApparelPOS.Services;
using ApparelPOS.ViewModels;
using ApparelPOS.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ApparelPOS;

public partial class App : Application
{
    private IServiceProvider _serviceProvider = null!;

    public App()
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // 1. Database
        services.AddDbContext<AppDbContext>();

        // 2. Core Business Services
        services.AddSingleton<IHardwareInfoService, HardwareInfoService>();
        services.AddSingleton<ILicenseService, LicenseService>();
        services.AddSingleton<IBarcodeService, BarcodeService>();
        services.AddSingleton<IPrinterService, PrinterService>();
        services.AddSingleton<ICameraScannerService, CameraScannerService>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<IBillingService, BillingService>();
        services.AddScoped<IAnalyticsService, AnalyticsService>();

        // 3. ViewModels
        services.AddTransient<ActivationViewModel>();
        services.AddTransient<BillingViewModel>();
        services.AddTransient<InventoryViewModel>();
        services.AddTransient<BarcodeGeneratorViewModel>();
        services.AddTransient<AnalyticsViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<MainViewModel>();

        // 4. Windows
        services.AddTransient<ActivationWindow>();
        services.AddTransient<MainWindow>();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Global unhandled exception logging/handling
        DispatcherUnhandledException += (s, args) =>
        {
            MessageBox.Show($"An unexpected error occurred: {args.Exception.Message}", "System Error", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        // 1. Initialize SQLite Database & Seed Data
        using (var scope = _serviceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            DbInitializer.Initialize(db);
        }

        // 2. Validate Hardware-Locked Cryptographic License
        var licenseService = _serviceProvider.GetRequiredService<ILicenseService>();
        var licenseInfo = licenseService.ValidateCurrentLicense();

        if (licenseInfo.IsValid)
        {
            // Valid license found: Launch Main Application
            LaunchMainWindow();
        }
        else
        {
            // Invalid or missing license: Launch Activation Dialog
            var activationWindow = _serviceProvider.GetRequiredService<ActivationWindow>();
            var result = activationWindow.ShowDialog();

            if (result == true)
            {
                LaunchMainWindow();
            }
            else
            {
                // User closed activation window without activating -> Exit
                Shutdown();
            }
        }
    }

    private void LaunchMainWindow()
    {
        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        MainWindow = mainWindow;
        mainWindow.Show();
    }
}
