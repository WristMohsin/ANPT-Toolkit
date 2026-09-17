using ANPT.Application.Interfaces;
using ANPT.Application.Models.Nmap;
using ANPT.Domain.Entities;
using ANPT.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ANPT.Infrastructure.Scanning;

/// <summary>
/// Maps <see cref="NmapScanResult"/> into EF entities and persists them transactionally
/// under the owning Scan. Reprocessing replaces prior result rows for that Scan only.
/// </summary>
public sealed class NmapResultPersistenceService : INmapResultPersistenceService
{
    private readonly AnptDbContext _db;
    private readonly ILogger<NmapResultPersistenceService> _logger;

    public NmapResultPersistenceService(AnptDbContext db, ILogger<NmapResultPersistenceService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<NmapPersistenceResult> PersistAsync(
        Guid scanId,
        NmapScanResult parsedResult,
        CancellationToken cancellationToken = default)
    {
        if (scanId == Guid.Empty)
            return NmapPersistenceResult.Failure("A scan id is required.");

        if (parsedResult is null)
            return NmapPersistenceResult.Failure("Parsed result is required.");

        var scan = await _db.Scans.FirstOrDefaultAsync(s => s.Id == scanId, cancellationToken)
            .ConfigureAwait(false);

        if (scan is null)
            return NmapPersistenceResult.Failure("Scan not found.");

        _logger.LogInformation("Beginning result persistence for Scan {ScanId}", scanId);

        await using var transaction = await _db.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        try
        {
            var existingHosts = await _db.Hosts
                .Where(h => h.ScanId == scanId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            if (existingHosts.Count > 0)
            {
                _db.Hosts.RemoveRange(existingHosts);
                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            scan.NmapScanner = Truncate(parsedResult.Scanner, 50);
            scan.NmapVersion = Truncate(parsedResult.Version, 50);
            scan.NmapArguments = Truncate(parsedResult.Arguments, 1000);
            scan.NmapElapsedSeconds = parsedResult.ElapsedSeconds;
            scan.NmapSummary = Truncate(parsedResult.Summary, 1000);
            scan.NmapExitStatus = Truncate(parsedResult.ExitStatus, 50);
            scan.UpdatedAt = DateTime.UtcNow;

            var now = DateTime.UtcNow;
            var hosts = new List<Host>();
            var portCount = 0;

            foreach (var nh in parsedResult.Hosts)
            {
                var host = MapHost(scanId, nh, now);
                hosts.Add(host);
                portCount += host.Services.Count;
            }

            if (hosts.Count > 0)
                _db.Hosts.AddRange(hosts);

            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Result persistence completed for Scan {ScanId}: {HostCount} hosts, {PortCount} ports",
                scanId, hosts.Count, portCount);

            return NmapPersistenceResult.Success(hosts.Count, portCount);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Result persistence cancelled for Scan {ScanId}", scanId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Result persistence failed for Scan {ScanId}", scanId);
            try
            {
                await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception rbEx)
            {
                _logger.LogError(rbEx, "Rollback failed for Scan {ScanId}", scanId);
            }

            return NmapPersistenceResult.Failure($"Persistence failed: {ex.Message}");
        }
    }

    private static Host MapHost(Guid scanId, NmapHostResult nh, DateTime now)
    {
        var ipv4 = nh.Addresses.FirstOrDefault(a =>
            string.Equals(a.AddressType, "ipv4", StringComparison.OrdinalIgnoreCase));
        var ipv6 = nh.Addresses.FirstOrDefault(a =>
            string.Equals(a.AddressType, "ipv6", StringComparison.OrdinalIgnoreCase));
        var mac = nh.Addresses.FirstOrDefault(a =>
            string.Equals(a.AddressType, "mac", StringComparison.OrdinalIgnoreCase));

        var preferredIp = ipv4?.Address
            ?? ipv6?.Address
            ?? string.Empty;

        var preferredHostname = nh.Hostnames
            .FirstOrDefault(h => string.Equals(h.Type, "PTR", StringComparison.OrdinalIgnoreCase))
            ?.Name
            ?? nh.Hostnames.FirstOrDefault()?.Name;

        var host = new Host
        {
            Id = Guid.NewGuid(),
            ScanId = scanId,
            IpAddress = Truncate(preferredIp, 45) ?? string.Empty,
            Hostname = Truncate(preferredHostname, 255),
            MacAddress = Truncate(mac?.Address, 50),
            Status = Truncate(nh.State, 50) ?? "unknown",
            StateReason = Truncate(nh.StateReason, 100),
            DiscoveredAt = now,
            CreatedAt = now
        };

        foreach (var addr in nh.Addresses)
        {
            if (string.IsNullOrWhiteSpace(addr.Address))
                continue;

            host.Addresses.Add(new HostAddress
            {
                Id = Guid.NewGuid(),
                HostId = host.Id,
                Address = Truncate(addr.Address, 100)!,
                AddressType = Truncate(addr.AddressType, 20) ?? "unknown",
                Vendor = Truncate(addr.Vendor, 200),
                CreatedAt = now
            });
        }

        foreach (var hn in nh.Hostnames)
        {
            if (string.IsNullOrWhiteSpace(hn.Name))
                continue;

            host.Hostnames.Add(new HostHostname
            {
                Id = Guid.NewGuid(),
                HostId = host.Id,
                Name = Truncate(hn.Name, 255)!,
                Type = Truncate(hn.Type, 50),
                CreatedAt = now
            });
        }

        foreach (var port in nh.Ports)
        {
            if (port.PortId is null)
                continue;

            var svc = port.Service;
            host.Services.Add(new Service
            {
                Id = Guid.NewGuid(),
                HostId = host.Id,
                Port = port.PortId.Value,
                Protocol = Truncate(port.Protocol, 10) ?? "tcp",
                State = Truncate(port.State, 20) ?? "unknown",
                StateReason = Truncate(port.Reason, 100),
                ServiceName = Truncate(svc?.Name, 100),
                Product = Truncate(svc?.Product, 200),
                Version = Truncate(svc?.Version, 100),
                Banner = Truncate(svc?.ExtraInfo, 2000),
                Tunnel = Truncate(svc?.Tunnel, 50),
                DetectionMethod = Truncate(svc?.Method, 50),
                Confidence = svc?.Confidence,
                DetectedAt = now,
                CreatedAt = now
            });
        }

        return host;
    }

    private static string? Truncate(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var trimmed = value.Trim();
        return trimmed.Length <= max ? trimmed : trimmed[..max];
    }
}
