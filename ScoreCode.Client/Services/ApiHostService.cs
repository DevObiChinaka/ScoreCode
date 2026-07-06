using System.Diagnostics;
using System.IO;

namespace ScoreCode.Client.Services;

/// <summary>
/// Launches the ScoreCode.Api ASP.NET process as a child of the WPF client.
/// The API process is killed automatically when the WPF app exits.
/// </summary>
public sealed class ApiHostService : IDisposable
{
    private Process? _apiProcess;

    /// <summary>
    /// Start the API if it is not already listening on port 5039.
    /// Returns immediately — the API boot takes a few seconds in the background.
    /// </summary>
    public void EnsureStarted()
    {
        // Don't start a second instance if one is already running
        if (IsPortInUse(5039))
        {
            System.Diagnostics.Debug.WriteLine("[ApiHostService] Port 5039 already in use — skipping.");
            return;
        }

        // Resolve API project path relative to the solution folder.
        // When running via `dotnet run`, BaseDirectory = bin/Debug/net10.0-windows/
        // Walk up to the solution root, then into ScoreCode.Api.
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var apiProjectPath = Path.GetFullPath(
            Path.Combine(baseDir, @"..\..\..\..\..\ScoreCode.Api\ScoreCode.Api.csproj"));

        System.Diagnostics.Debug.WriteLine($"[ApiHostService] BaseDir: {baseDir}");
        System.Diagnostics.Debug.WriteLine($"[ApiHostService] Resolved API path: {apiProjectPath}");

        if (!File.Exists(apiProjectPath))
        {
            // Fallback: try relative to CWD (for cases run from solution root)
            apiProjectPath = Path.GetFullPath(
                Path.Combine(Directory.GetCurrentDirectory(), @"ScoreCode.Api\ScoreCode.Api.csproj"));
            System.Diagnostics.Debug.WriteLine($"[ApiHostService] Fallback path: {apiProjectPath}");

            if (!File.Exists(apiProjectPath))
            {
                System.Diagnostics.Debug.WriteLine("[ApiHostService] API project file NOT FOUND — cannot start API.");
                return;
            }
        }

        var psi = new ProcessStartInfo
        {
            FileName               = "dotnet",
            Arguments              = $"run --project \"{apiProjectPath}\" --urls http://localhost:5039",
            UseShellExecute        = false,
            CreateNoWindow         = true,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
        };

        _apiProcess = new Process { StartInfo = psi, EnableRaisingEvents = true };
        _apiProcess.OutputDataReceived += (_, e) =>
            System.Diagnostics.Debug.WriteLine($"[API] {e.Data}");
        _apiProcess.ErrorDataReceived += (_, e) =>
            System.Diagnostics.Debug.WriteLine($"[API ERR] {e.Data}");
        _apiProcess.Exited += (_, _) =>
            System.Diagnostics.Debug.WriteLine("[ApiHostService] API process exited.");

        _apiProcess.Start();
        _apiProcess.BeginOutputReadLine();
        _apiProcess.BeginErrorReadLine();

        System.Diagnostics.Debug.WriteLine(
            $"[ApiHostService] API started — PID {_apiProcess.Id}");
    }

    /// <summary>Kill the hosted API process on application exit.</summary>
    public void Dispose()
    {
        try
        {
            if (_apiProcess is not null && !_apiProcess.HasExited)
            {
                _apiProcess.Kill(entireProcessTree: true);
                _apiProcess.WaitForExit(3000);
            }
        }
        catch { /* best effort */ }
        finally
        {
            _apiProcess?.Dispose();
        }
    }

    // ── helpers ────────────────────────────────────────────────────────
    private static bool IsPortInUse(int port)
    {
        try
        {
            var props = System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties();
            return props.GetActiveTcpListeners()
                        .Any(ep => ep.Port == port);
        }
        catch { return false; }
    }
}
