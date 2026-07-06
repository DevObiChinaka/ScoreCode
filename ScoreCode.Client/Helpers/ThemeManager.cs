using System.IO;
using System.Windows;
using System.Windows.Media;

namespace ScoreCode.Client.Helpers;

public enum AppTheme { Dark, Light }

/// <summary>
/// Singleton that manages the app theme (Dark / Light).
/// Swaps ResourceDictionary in App.Current.Resources and persists preference.
/// Code-behind can use <see cref="GetColor"/> / <see cref="GetBrush"/> for dynamic UI.
/// </summary>
public static class ThemeManager
{
    private static readonly string _settingsPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ScoreCode", "theme.txt");

    private static ResourceDictionary? _currentThemeDict;

    public static AppTheme CurrentTheme { get; private set; } = AppTheme.Dark;

    /// <summary>Raised after the theme changes so code-behind can refresh colours.</summary>
    public static event Action? ThemeChanged;

    /// <summary>Call once at app startup to load persisted preference.</summary>
    public static void Initialize()
    {
        var saved = LoadSavedTheme();
        ApplyTheme(saved, persist: false);
    }

    /// <summary>Switch to a specific theme.</summary>
    public static void ApplyTheme(AppTheme theme, bool persist = true)
    {
        CurrentTheme = theme;

        var uri = theme == AppTheme.Light
            ? new Uri("Assets/Themes/LightTheme.xaml", UriKind.Relative)
            : new Uri("Assets/Themes/DarkTheme.xaml", UriKind.Relative);

        var newDict = new ResourceDictionary { Source = uri };

        var app = Application.Current;
        if (_currentThemeDict is not null)
            app.Resources.MergedDictionaries.Remove(_currentThemeDict);

        app.Resources.MergedDictionaries.Insert(0, newDict);
        _currentThemeDict = newDict;

        if (persist) SaveTheme(theme);

        ThemeChanged?.Invoke();
    }

    /// <summary>Toggle between Dark and Light.</summary>
    public static void Toggle()
    {
        ApplyTheme(CurrentTheme == AppTheme.Dark ? AppTheme.Light : AppTheme.Dark);
    }

    // ── Helpers for code-behind ───────────────────────────────────────────

    /// <summary>Get a theme Color by key (e.g. "ThBgBase").</summary>
    public static Color GetColor(string key)
    {
        if (Application.Current.TryFindResource(key) is Color c) return c;
        return Colors.Magenta; // obvious fallback for missing keys
    }

    /// <summary>Get a SolidColorBrush from a theme Color key.</summary>
    public static SolidColorBrush GetBrush(string key) => new(GetColor(key));

    // ── Persistence ──────────────────────────────────────────────────────

    private static AppTheme LoadSavedTheme()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var text = File.ReadAllText(_settingsPath).Trim();
                if (Enum.TryParse<AppTheme>(text, true, out var t)) return t;
            }
        }
        catch { /* ignore */ }
        return AppTheme.Dark;
    }

    private static void SaveTheme(AppTheme theme)
    {
        try
        {
            var dir = Path.GetDirectoryName(_settingsPath)!;
            Directory.CreateDirectory(dir);
            File.WriteAllText(_settingsPath, theme.ToString());
        }
        catch { /* ignore */ }
    }
}
