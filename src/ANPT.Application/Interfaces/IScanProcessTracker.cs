namespace ANPT.Application.Interfaces;

/// <summary>
/// Tracks in-flight Nmap executions so Cancel can terminate the correct process tree
/// without cross-scan interference. Singleton lifetime.
/// </summary>
public interface IScanProcessTracker
{
    /// <summary>
    /// Registers a running execution. Returns false if the scan is already registered
    /// (duplicate start protection).
    /// </summary>
    bool TryRegister(Guid scanId, CancellationTokenSource linkedCts);

    /// <summary>
    /// Requests cancellation of a registered execution. Returns true if a CTS was found and cancelled.
    /// </summary>
    bool TryCancel(Guid scanId);

    /// <summary>
    /// Removes registration after process exit (success, failure, or cancel).
    /// </summary>
    void Unregister(Guid scanId);

    /// <summary>
    /// True if an execution is currently registered for the scan.
    /// </summary>
    bool IsRegistered(Guid scanId);
}
