using ANPT.Application.Models.Nmap;

namespace ANPT.Application.Interfaces;

/// <summary>
/// Persists a parsed <see cref="NmapScanResult"/> into the database for a specific Scan.
/// Does not execute Nmap or parse XML — works from already-parsed models only.
/// </summary>
public interface INmapResultPersistenceService
{
    /// <summary>
    /// Atomically replaces any existing result rows for the scan with the given parsed result.
    /// </summary>
    Task<NmapPersistenceResult> PersistAsync(
        Guid scanId,
        NmapScanResult parsedResult,
        CancellationToken cancellationToken = default);
}

public sealed class NmapPersistenceResult
{
    public bool Succeeded { get; private init; }
    public string? ErrorMessage { get; private init; }
    public int HostCount { get; private init; }
    public int PortCount { get; private init; }

    public static NmapPersistenceResult Success(int hostCount, int portCount) =>
        new() { Succeeded = true, HostCount = hostCount, PortCount = portCount };

    public static NmapPersistenceResult Failure(string message) =>
        new() { Succeeded = false, ErrorMessage = message };
}
