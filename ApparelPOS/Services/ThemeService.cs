using System.IO;
using System.Text.Json;
using System.Windows;

namespace ApparelPOS.Services;

public class ThemeService : IThemeService
{
    private static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ApparelPOS");

    private static readonly string ConfigPath = Path.Combine(ConfigDir, "theme.json");

    public AppTheme CurrentTheme { get; private set; } = AppTheme.ModernDark;

    public event Action<AppTheme>? ThemeChanged;

    private readonly List<ThemeOption> _availableThemes;
    public IReadOnlyList<ThemeOption> AvailableThemes => _availableThemes;

    public ThemeOption CurrentThemeOption =>
        _availableThemes.FirstOrDefault(t => t.Id == CurrentTheme) ?? _availableThemes[0];

    public ThemeService()
    {
        _availableThemes = new List<ThemeOption>
        {
            new()
            {
                Id = AppTheme.ModernDark,
                Name = "Modern Dark",
                Description = "Default sleek dark slate mode with vibrant indigo accents for low eye-strain.",
                Icon = "🌙",
                PrimaryPreviewHex = "#6366F1",
                BgPreviewHex = "#0F172A",
                CardPreviewHex = "#1E293B",
                TextPreviewHex = "#F8FAFC",
                BorderPreviewHex = "#334155"
            },
            new()
            {
                Id = AppTheme.CrispLight,
                Name = "Crisp Light",
                Description = "Ultra-clean high-contrast retail light mode with crisp borders and bold dark text.",
                Icon = "☀️",
                PrimaryPreviewHex = "#4F46E5",
                BgPreviewHex = "#F1F5F9",
                CardPreviewHex = "#FFFFFF",
                TextPreviewHex = "#0F172A",
                BorderPreviewHex = "#CBD5E1"
            },
            new()
            {
                Id = AppTheme.OceanNavy,
                Name = "Ocean Navy",
                Description = "Deep oceanic midnight blue surfaces paired with vivid cyan and sky highlights.",
                Icon = "🌊",
                PrimaryPreviewHex = "#0284C7",
                BgPreviewHex = "#0A1128",
                CardPreviewHex = "#101F42",
                TextPreviewHex = "#F0F6FF",
                BorderPreviewHex = "#1E3A70"
            },
            new()
            {
                Id = AppTheme.EmeraldLuxury,
                Name = "Emerald Luxury",
                Description = "Sophisticated forest green boutique aesthetic with rich emerald & gold accents.",
                Icon = "🌲",
                PrimaryPreviewHex = "#10B981",
                BgPreviewHex = "#051B14",
                CardPreviewHex = "#0C2E23",
                TextPreviewHex = "#F0FDF4",
                BorderPreviewHex = "#19503E"
            },
            new()
            {
                Id = AppTheme.RoyalVelvet,
                Name = "Royal Velvet",
                Description = "Regal velvet plum and purple palette tailored for Velvet Thread Apparel.",
                Icon = "👑",
                PrimaryPreviewHex = "#9333EA",
                BgPreviewHex = "#150D22",
                CardPreviewHex = "#221437",
                TextPreviewHex = "#FAF5FF",
                BorderPreviewHex = "#3E2363"
            }
        };
    }

    public void InitializeTheme()
    {
        var saved = LoadSavedTheme();
        ApplyTheme(saved);
    }

    public void ApplyTheme(AppTheme theme)
    {
        string fileName = theme switch
        {
            AppTheme.ModernDark => "ThemeModernDark.xaml",
            AppTheme.CrispLight => "ThemeCrispLight.xaml",
            AppTheme.OceanNavy => "ThemeOceanNavy.xaml",
            AppTheme.EmeraldLuxury => "ThemeEmeraldLuxury.xaml",
            AppTheme.RoyalVelvet => "ThemeRoyalVelvet.xaml",
            _ => "ThemeModernDark.xaml"
        };

        try
        {
            var uri = new Uri($"/Styles/Themes/{fileName}", UriKind.Relative);
            var themeDict = new ResourceDictionary { Source = uri };

            if (Application.Current != null)
            {
                // Update direct keys to immediately trigger all DynamicResource re-evaluations
                foreach (var key in themeDict.Keys)
                {
                    Application.Current.Resources[key] = themeDict[key];
                }

                // Also update merged dictionaries
                var existingDict = Application.Current.Resources.MergedDictionaries
                    .FirstOrDefault(d => d.Source != null && (d.Source.OriginalString.Contains("Theme") || d.Source.OriginalString.Contains("Colors.xaml")));

                if (existingDict != null)
                {
                    int index = Application.Current.Resources.MergedDictionaries.IndexOf(existingDict);
                    Application.Current.Resources.MergedDictionaries[index] = themeDict;
                }
                else
                {
                    Application.Current.Resources.MergedDictionaries.Insert(0, themeDict);
                }
            }

            CurrentTheme = theme;
            foreach (var opt in _availableThemes)
            {
                opt.IsActive = (opt.Id == theme);
            }

            SaveTheme(theme);
            ThemeChanged?.Invoke(theme);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to apply theme {theme}: {ex.Message}");
        }
    }

    private AppTheme LoadSavedTheme()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("Theme", out var prop))
                {
                    var val = prop.GetString();
                    if (Enum.TryParse<AppTheme>(val, true, out var parsedTheme))
                    {
                        return parsedTheme;
                    }
                }
            }
        }
        catch
        {
            // fallback to default
        }
        return AppTheme.ModernDark;
    }

    private void SaveTheme(AppTheme theme)
    {
        try
        {
            if (!Directory.Exists(ConfigDir))
            {
                Directory.CreateDirectory(ConfigDir);
            }
            var json = JsonSerializer.Serialize(new { Theme = theme.ToString() });
            File.WriteAllText(ConfigPath, json);
        }
        catch
        {
            // Fail safe
        }
    }
}

