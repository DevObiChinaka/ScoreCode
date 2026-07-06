using System.IO;
using System.Windows;

namespace ScoreCode.Client.Helpers;

/// <summary>
/// Saves and restores the main window state (Maximized / Normal) to a plain‐text
/// settings file in %LocalAppData%\ScoreCode\window.state.
/// </summary>
public static class WindowStateHelper
{
    private static readonly string StatePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ScoreCode", "window.state");

    /// <summary>Persist the current WindowState.</summary>
    public static void Save(WindowState state)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(StatePath)!);
            File.WriteAllText(StatePath, state == WindowState.Maximized ? "Maximized" : "Normal");
        }
        catch { /* best-effort — never crash for a preference */ }
    }

    /// <summary>Read the previously saved state (defaults to Normal).</summary>
    public static WindowState Load()
    {
        try
        {
            if (File.Exists(StatePath))
            {
                var text = File.ReadAllText(StatePath).Trim();
                return text == "Maximized" ? WindowState.Maximized : WindowState.Normal;
            }
        }
        catch { /* corrupted file — fall through */ }
        return WindowState.Normal;
    }
}
