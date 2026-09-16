using System.Collections.Concurrent;
using ANPT.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace ANPT.Infrastructure.Scanning;

/// <summary>
/// Thread-safe registry of active scan executions and their cancellation sources.
/// </summary>
public sealed class ScanProcessTracker : IScanProcessTracker
{
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _active = new();
    private readonly ILogger<ScanProcessTracker> _logger;

    public ScanProcessTracker(ILogger<ScanProcessTracker> logger)
    {
        _logger = logger;
    }

    public bool TryRegister(Guid scanId, CancellationTokenSource linkedCts)
    {
        if (scanId == Guid.Empty || linkedCts is null)
            return false;

        if (!_active.TryAdd(scanId, linkedCts))
        {
            _logger.LogWarning("Duplicate execution registration rejected for scan {ScanId}", scanId);
            return false;
        }

        _logger.LogDebug("Registered active execution for scan {ScanId}", scanId);
        return true;
    }

    public bool TryCancel(Guid scanId)
    {
        if (!_active.TryGetValue(scanId, out var cts))
            return false;

        try
        {
            if (!cts.IsCancellationRequested)
            {
                _logger.LogInformation("Requesting cancellation of active Nmap process for scan {ScanId}", scanId);
                cts.Cancel();
            }
            return true;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cancel CTS for scan {ScanId}", scanId);
            return false;
        }
    }

    public void Unregister(Guid scanId)
    {
        if (_active.TryRemove(scanId, out var cts))
        {
            try { cts.Dispose(); } catch { /* ignore */ }
            _logger.LogDebug("Unregistered execution for scan {ScanId}", scanId);
        }
    }

    public bool IsRegistered(Guid scanId) => _active.ContainsKey(scanId);
}
