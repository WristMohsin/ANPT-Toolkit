namespace ANPT.Application.Interfaces;

/// <summary>
/// Application-level scan execution: re-validates authorization, builds safe args, invokes Nmap runner.
/// </summary>
public interface IScanExecutionService
{
    Task<NmapAvailabilityResult> CheckNmapAvailabilityAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts a Queued scan: re-checks eligibility, marks Running, runs Nmap, then Finalizes status.
    /// </summary>
    Task<ScanExecutionResult> StartAsync(Guid scanId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Requests cancellation of an in-flight Nmap process for a Running scan.
    /// Returns true if a process was registered and cancel was signalled.
    /// </summary>
    bool RequestCancel(Guid scanId);
}

public sealed class ScanExecutionResult
{
    public bool Succeeded { get; private init; }
    public string? ErrorMessage { get; private init; }
    public Guid? ScanId { get; private init; }
    public NmapRunResult? ProcessResult { get; private init; }

    public static ScanExecutionResult Success(Guid scanId, NmapRunResult processResult) =>
        new() { Succeeded = true, ScanId = scanId, ProcessResult = processResult };

    public static ScanExecutionResult Failure(string message) =>
        new() { Succeeded = false, ErrorMessage = message };
}
