namespace ApparelPOS.Services;

public enum AppTheme
{
    ModernDark,
    CrispLight,
    OceanNavy,
    EmeraldLuxury,
    RoyalVelvet
}

public class ThemeOption
{
    public AppTheme Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string PrimaryPreviewHex { get; set; } = string.Empty;
    public string BgPreviewHex { get; set; } = string.Empty;
    public string CardPreviewHex { get; set; } = string.Empty;
    public string TextPreviewHex { get; set; } = string.Empty;
    public string BorderPreviewHex { get; set; } = string.Empty;
    public bool IsActive { get; set; }

    public string DisplayTitle => $"{Icon} {Name}";
}

public interface IThemeService
{
    AppTheme CurrentTheme { get; }
    ThemeOption CurrentThemeOption { get; }
    IReadOnlyList<ThemeOption> AvailableThemes { get; }
    event Action<AppTheme>? ThemeChanged;
    void ApplyTheme(AppTheme theme);
    void InitializeTheme();
}

