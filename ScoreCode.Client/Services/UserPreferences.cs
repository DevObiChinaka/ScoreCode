using System.IO;
using System.Text.Json;

namespace ScoreCode.Client.Services;

/// <summary>
/// Persists user preferences to %LocalAppData%\ScoreCode\preferences.json.
/// </summary>
public static class UserPreferences
{
    private static readonly string _filePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ScoreCode", "preferences.json");

    private static Dictionary<string, JsonElement> _cache = [];

    static UserPreferences()
    {
        Load();
    }

    private static void Load()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                _cache = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json) ?? [];
            }
        }
        catch { _cache = []; }
    }

    private static void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(_filePath)!;
            Directory.CreateDirectory(dir);
            File.WriteAllText(_filePath,
                JsonSerializer.Serialize(_cache, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* graceful failure */ }
    }

    public static bool GetBool(string key, bool defaultValue = true)
    {
        if (_cache.TryGetValue(key, out var el) && (el.ValueKind == JsonValueKind.True || el.ValueKind == JsonValueKind.False))
            return el.GetBoolean();
        return defaultValue;
    }

    public static string GetString(string key, string defaultValue = "")
    {
        if (_cache.TryGetValue(key, out var el) && el.ValueKind == JsonValueKind.String)
            return el.GetString() ?? defaultValue;
        return defaultValue;
    }

    public static int GetInt(string key, int defaultValue = 0)
    {
        if (_cache.TryGetValue(key, out var el) && el.ValueKind == JsonValueKind.Number)
            return el.GetInt32();
        return defaultValue;
    }

    public static void Set(string key, bool value)
    {
        _cache[key] = JsonSerializer.SerializeToElement(value);
        Save();
    }

    public static void Set(string key, string value)
    {
        _cache[key] = JsonSerializer.SerializeToElement(value);
        Save();
    }

    public static void Set(string key, int value)
    {
        _cache[key] = JsonSerializer.SerializeToElement(value);
        Save();
    }
}
