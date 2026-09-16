namespace ANPT.Application.Interfaces;

/// <summary>
/// Isolates Nmap process launch. Application code must not call Process.Start directly.
/// </summary>
public interface INmapProcessRunner
{
    /// <summary>
    /// Harmless local check: locate nmap and optionally run --version. No network scan.
    /// </summary>
    Task<NmapAvailabilityResult> CheckAvailabilityAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs nmap with a controlled executable path and allow-listed ArgumentList only.
    /// </summary>
    Task<NmapRunResult> RunAsync(NmapRunRequest request, CancellationToken cancellationToken = default);
}

public sealed class NmapAvailabilityResult
{
    public bool IsAvailable { get; init; }
    public string? ExecutablePath { get; init; }
    public string? VersionText { get; init; }
    public string? ErrorMessage { get; init; }

    public static NmapAvailabilityResult Available(string path, string? version) =>
        new() { IsAvailable = true, ExecutablePath = path, VersionText = version };

    public static NmapAvailabilityResult Unavailable(string message) =>
        new() { IsAvailable = false, ErrorMessage = message };
}

public sealed class NmapRunRequest
{
    /// <summary>Validated absolute path to nmap.exe (or nmap).</summary>
    public required string ExecutablePath { get; init; }

    /// <summary>Discrete process arguments only — never a shell command string.</summary>
    public required IReadOnlyList<string> Arguments { get; init; }

    /// <summary>Optional controlled XML output path under application temp.</summary>
    public string? XmlOutputPath { get; init; }

    /// <summary>Working directory for the process (application-controlled).</summary>
    public string? WorkingDirectory { get; init; }
}

public sealed class NmapRunResult
{
    public bool Started { get; init; }
    public bool Completed { get; init; }
    public bool Cancelled { get; init; }
    public bool Failed { get; init; }
    public int? ExitCode { get; init; }
    public string StandardOutput { get; init; } = string.Empty;
    public string StandardError { get; init; } = string.Empty;
    public TimeSpan Duration { get; init; }
    public string? ErrorMessage { get; init; }
    public string? XmlOutputPath { get; init; }

    public static NmapRunResult StartupFailure(string message) =>
        new() { Failed = true, ErrorMessage = message };

    public static NmapRunResult FromProcess(
        int exitCode,
        string stdout,
        string stderr,
        TimeSpan duration,
        bool cancelled,
        string? xmlPath) =>
        new()
        {
            Started = true,
            Completed = !cancelled && exitCode == 0,
            Cancelled = cancelled,
            Failed = !cancelled && exitCode != 0,
            ExitCode = exitCode,
            StandardOutput = stdout,
            StandardError = stderr,
            Duration = duration,
            XmlOutputPath = xmlPath,
            ErrorMessage = cancelled
                ? "Process cancelled."
                : exitCode != 0
                    ? $"Nmap exited with code {exitCode}."
                    : null
        };
}
