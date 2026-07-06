namespace ScoreCode.Client.Services;

/// <summary>Result of a local code execution.</summary>
public class ExecutionResult
{
    public string Stdout { get; set; } = string.Empty;
    public string Stderr { get; set; } = string.Empty;
    public int ExitCode { get; set; }
    public bool TimedOut { get; set; }
    public bool MemoryExceeded { get; set; }
    public string? CompilationError { get; set; }
    public double ExecutionTimeMs { get; set; }

    public bool Success => ExitCode == 0 && !TimedOut && !MemoryExceeded && CompilationError is null;
}
