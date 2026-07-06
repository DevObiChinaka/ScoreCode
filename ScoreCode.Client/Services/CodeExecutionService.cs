using System.Diagnostics;
using System.IO;
using System.Text;

namespace ScoreCode.Client.Services;

/// <summary>
/// Executes student code locally using RuntimePaths and Windows Job Objects.
/// Supports Python, Java, C++, JavaScript. HTML/CSS are handled separately via WebView2.
/// </summary>
public static class CodeExecutionService
{
    private const int WallClockTimeoutMs = 10_000;   // 10 seconds wall-clock
    private const int CpuLimitSeconds    = 5;         // 5 seconds CPU
    private const long MemoryLimitBytes  = 256 * 1024 * 1024; // 256 MB
    private const int MaxOutputChars     = 10_000;    // 10 KB output cap

    /// <summary>Execute code in the given language and return the result.</summary>
    public static async Task<ExecutionResult> ExecuteAsync(string code, string language,
        string stdin = "", CancellationToken ct = default)
    {
        var lang = language.ToLowerInvariant();

        // HTML/CSS are not "executed" — they are returned as-is for WebView2 rendering
        if (lang is "html" or "css")
        {
            return new ExecutionResult
            {
                Stdout = code,
                ExitCode = 0,
                ExecutionTimeMs = 0
            };
        }

        // Validate that the required runtime is available before trying to run
        var runtimeCheck = ValidateRuntime(lang);
        if (runtimeCheck is not null)
        {
            return new ExecutionResult
            {
                Stderr = runtimeCheck,
                ExitCode = -1,
                ExecutionTimeMs = 0
            };
        }

        var tempDir = Path.Combine(Path.GetTempPath(), "ScoreCode", $"exec_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            // ── Step 1: Write source file ─────────────────────────────────
            var (sourceFile, compileArgs, runExe, runArgs) = PrepareLanguage(lang, tempDir);
            await File.WriteAllTextAsync(Path.Combine(tempDir, sourceFile), code, ct);

            // ── Step 2: Compile (Java / C++) ──────────────────────────────
            if (compileArgs is not null)
            {
                var compileResult = await RunProcessAsync(
                    compileArgs.Value.exe, compileArgs.Value.args, tempDir, stdin: "", ct);

                if (compileResult.ExitCode != 0)
                {
                    return new ExecutionResult
                    {
                        CompilationError = string.IsNullOrWhiteSpace(compileResult.Stderr)
                            ? compileResult.Stdout
                            : compileResult.Stderr,
                        ExitCode = compileResult.ExitCode,
                        ExecutionTimeMs = compileResult.ExecutionTimeMs
                    };
                }
            }

            // ── Step 3: Run ───────────────────────────────────────────────
            return await RunProcessAsync(runExe, runArgs, tempDir, stdin, ct);
        }
        finally
        {
            // ── Step 4: Cleanup ───────────────────────────────────────────
            try { Directory.Delete(tempDir, recursive: true); }
            catch { /* best-effort cleanup */ }
        }
    }

    /// <summary>
    /// Check that the required runtime for a language is available.
    /// Returns null if OK, or an error message string if missing.
    /// </summary>
    private static string? ValidateRuntime(string lang)
    {
        var checks = lang switch
        {
            "python"              => new[] { ("Python", RuntimePaths.Python) },
            "javascript" or "js"  => new[] { ("Node.js", RuntimePaths.Node) },
            "java"                => new[] { ("Java compiler (javac)", RuntimePaths.Javac),
                                             ("Java runtime (java)", RuntimePaths.Java) },
            "cpp" or "c++"       => new[] { ("C++ compiler (g++)", RuntimePaths.Gpp) },
            _                     => null
        };

        if (checks is null)
            return $"Language '{lang}' is not supported for execution.";

        foreach (var (name, path) in checks)
        {
            // Bundled path — check file exists directly
            if (Path.IsPathRooted(path))
            {
                if (!File.Exists(path))
                    return $"{name} not found at bundled path:\n{path}\n\nEnsure runtimes are installed.";
            }
            else
            {
                // System PATH — try to resolve
                if (!IsOnPath(path))
                    return $"{name} ({path}) is not installed or not on the system PATH.\n\n" +
                           $"Install it and restart the application.";
            }
        }

        return null; // All good
    }

    /// <summary>Check if an executable is findable on the system PATH.</summary>
    /// <remarks>
    /// Reads PATH fresh from the registry (Machine + User) rather than the
    /// process-inherited PATH, so newly installed runtimes are detected
    /// even if the IDE was started before they were installed.
    /// </remarks>
    private static bool IsOnPath(string exeName)
    {
        // Read fresh PATH from registry — not the stale process environment
        var machinePath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine) ?? "";
        var userPath    = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? "";
        var freshPath   = $"{machinePath}{Path.PathSeparator}{userPath}";

        foreach (var dir in freshPath.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var full = Path.Combine(dir, exeName);
                if (File.Exists(full)) return true;
                // Windows: also check with .exe extension
                if (!exeName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                    && File.Exists(full + ".exe"))
                    return true;
            }
            catch { /* skip invalid PATH entries */ }
        }
        return false;
    }

    /// <summary>
    /// Determine file names, compile command, and run command per language.
    /// </summary>
    private static (string sourceFile, (string exe, string args)? compileArgs,
        string runExe, string runArgs) PrepareLanguage(string lang, string tempDir)
    {
        return lang switch
        {
            "python" => (
                "main.py",
                null,
                RuntimePaths.Python,
                $"\"{Path.Combine(tempDir, "main.py")}\""
            ),
            "javascript" or "js" => (
                "main.js",
                null,
                RuntimePaths.Node,
                $"\"{Path.Combine(tempDir, "main.js")}\""
            ),
            "java" => (
                "Main.java",
                (RuntimePaths.Javac, $"\"{Path.Combine(tempDir, "Main.java")}\""),
                RuntimePaths.Java,
                $"-cp \"{tempDir}\" Main"
            ),
            "cpp" or "c++" => (
                "main.cpp",
                (RuntimePaths.Gpp, $"\"{Path.Combine(tempDir, "main.cpp")}\" -o \"{Path.Combine(tempDir, "main.exe")}\""),
                Path.Combine(tempDir, "main.exe"),
                ""
            ),
            _ => throw new NotSupportedException($"Language '{lang}' is not supported for execution.")
        };
    }

    /// <summary>
    /// Start a process with Job Object limits, capture stdout/stderr.
    /// </summary>
    private static async Task<ExecutionResult> RunProcessAsync(
        string exe, string args, string workingDir, string stdin, CancellationToken ct)
    {
        var result = new ExecutionResult();
        var sw = Stopwatch.StartNew();

        var psi = new ProcessStartInfo
        {
            FileName = exe,
            Arguments = args,
            WorkingDirectory = workingDir,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        // Inject fresh PATH from registry so runtimes installed after IDE launch are found
        var machinePath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine) ?? "";
        var userPath    = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? "";
        psi.Environment["PATH"] = $"{machinePath}{Path.PathSeparator}{userPath}";

        using var process = new Process { StartInfo = psi };
        using var job = new JobObjectHelper(CpuLimitSeconds, MemoryLimitBytes);

        var stdoutBuilder = new StringBuilder();
        var stderrBuilder = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null && stdoutBuilder.Length < MaxOutputChars)
                stdoutBuilder.AppendLine(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null && stderrBuilder.Length < MaxOutputChars)
                stderrBuilder.AppendLine(e.Data);
        };

        try
        {
            process.Start();
            job.AssignProcess(process);

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Write stdin if provided
            if (!string.IsNullOrEmpty(stdin))
            {
                await process.StandardInput.WriteAsync(stdin);
            }
            process.StandardInput.Close();

            // Wait with wall-clock timeout
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(WallClockTimeoutMs);

            try
            {
                await process.WaitForExitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException)
            {
                // Wall-clock timeout — kill if still running
                try { process.Kill(entireProcessTree: true); } catch { }
                result.TimedOut = true;
            }

            sw.Stop();
            result.ExecutionTimeMs = sw.Elapsed.TotalMilliseconds;
            result.Stdout = stdoutBuilder.ToString().TrimEnd();
            result.Stderr = stderrBuilder.ToString().TrimEnd();

            if (!result.TimedOut)
            {
                result.ExitCode = process.ExitCode;

                // Check if Job Object killed it for resource violation
                var (timedOut, memExceeded) = job.GetExitReason(process);
                if (timedOut) result.TimedOut = true;
                if (memExceeded) result.MemoryExceeded = true;
            }

            // Truncate if too long
            if (result.Stdout.Length > MaxOutputChars)
                result.Stdout = result.Stdout[..MaxOutputChars] + "\n... (output truncated)";
            if (result.Stderr.Length > MaxOutputChars)
                result.Stderr = result.Stderr[..MaxOutputChars] + "\n... (output truncated)";
        }
        catch (Exception ex)
        {
            sw.Stop();
            result.Stderr = $"Failed to execute: {ex.Message}";
            result.ExitCode = -1;
            result.ExecutionTimeMs = sw.Elapsed.TotalMilliseconds;
        }

        return result;
    }
}
