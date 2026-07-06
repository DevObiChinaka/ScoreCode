using System.IO;

namespace ScoreCode.Client.Services;

/// <summary>
/// Resolves runtime executable paths using dual-mode detection.
/// If the bundled runtimes marker exists → bundled paths.
/// Otherwise → scans the system PATH (fresh from registry) to find full paths.
/// </summary>
public static class RuntimePaths
{
    private static readonly string AppDir = AppDomain.CurrentDomain.BaseDirectory;
    private static readonly string RuntimesDir = Path.Combine(AppDir, "runtimes");
    /// <summary>
    /// True when our custom bundled runtimes are present (not the NuGet runtimes/ folder).
    /// Checks for a marker file that the Inno Setup installer creates.
    /// </summary>
    public static bool UseBundled =>
        File.Exists(Path.Combine(RuntimesDir, "scorecode-runtimes.marker"));

    // ── Resolved paths ───────────────────────────────────────────────────
    public static string Python => Resolve("python", "python-3.12", "python.exe");
    public static string Node   => Resolve("node",   "node-20",     "node.exe");
    public static string Java   => Resolve("java",   "jdk-21",      "bin", "java.exe");
    public static string Javac  => Resolve("javac",  "jdk-21",      "bin", "javac.exe");
    public static string Gpp    => Resolve("g++",    "mingw64",     "bin", "g++.exe");

    /// <summary>
    /// If bundled → join runtimesDir + segments and return absolute path.
    /// Otherwise → scan the fresh registry PATH for the executable and return its full path.
    /// Falls back to just the system name if not found (will give a clear error later).
    /// </summary>
    private static string Resolve(string systemName, params string[] bundledSegments)
    {
        if (UseBundled)
            return Path.Combine(RuntimesDir, Path.Combine(bundledSegments));

        // Resolve full path from registry PATH (not the stale process PATH)
        return FindOnRegistryPath(systemName) ?? systemName;
    }

    /// <summary>
    /// Reads PATH fresh from Machine + User registry and searches for an executable.
    /// Returns the full absolute path, or null if not found.
    /// </summary>
    private static string? FindOnRegistryPath(string exeName)
    {
        var machinePath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine) ?? "";
        var userPath    = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? "";
        var freshPath   = $"{machinePath}{Path.PathSeparator}{userPath}";

        foreach (var dir in freshPath.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                // Try exact name
                var full = Path.Combine(dir, exeName);
                if (File.Exists(full)) return full;

                // Try with .exe extension
                if (!exeName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    var withExe = full + ".exe";
                    if (File.Exists(withExe)) return withExe;
                }
            }
            catch { /* skip invalid PATH entries */ }
        }

        return null;
    }
}
