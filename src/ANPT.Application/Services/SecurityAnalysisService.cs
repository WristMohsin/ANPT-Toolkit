using System.Diagnostics;
using ANPT.Application.Interfaces;
using ANPT.Domain.Entities;
using ANPT.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace ANPT.Application.Services;

/// <summary>
/// Loads the persisted host/service graph for a scan.
/// Implemented in Infrastructure; kept in Application so analysis stays free of EF.
/// </summary>
public interface IScanHostLoader
{
    Task<IReadOnlyList<Host>> GetHostsForScanAsync(Guid scanId, CancellationToken cancellationToken = default);
}

public sealed class SecurityAnalysisService : ISecurityAnalysisService
{
    private readonly IScanRepository _scans;
    private readonly IFindingRepository _findings;
    private readonly IScanHostLoader _hostLoader;
    private readonly IEnumerable<IAnalysisRule> _rules;
    private readonly ILogger<SecurityAnalysisService> _logger;

    public SecurityAnalysisService(
        IScanRepository scans,
        IFindingRepository findings,
        IScanHostLoader hostLoader,
        IEnumerable<IAnalysisRule> rules,
        ILogger<SecurityAnalysisService> logger)
    {
        _scans = scans;
        _findings = findings;
        _hostLoader = hostLoader;
        _rules = rules;
        _logger = logger;
    }

    public async Task<SecurityAnalysisResult> AnalyzeScanAsync(Guid scanId, CancellationToken cancellationToken = default)
    {
        if (scanId == Guid.Empty)
            return SecurityAnalysisResult.Failure("A scan id is required.");

        var sw = Stopwatch.StartNew();

        var scan = await _scans.GetByIdAsync(scanId, cancellationToken).ConfigureAwait(false);
        if (scan is null)
            return SecurityAnalysisResult.Failure("Scan not found.");

        if (scan.Status != ScanStatus.Completed)
        {
            return SecurityAnalysisResult.Failure(
                $"Analysis requires a completed scan. Current status is {scan.Status}.");
        }

        _logger.LogInformation("Starting security analysis for Scan {ScanId}", scanId);

        IReadOnlyList<Host> hosts;
        try
        {
            hosts = await _hostLoader.GetHostsForScanAsync(scanId, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load hosts for Scan {ScanId}", scanId);
            return SecurityAnalysisResult.Failure("Failed to load scan host data.");
        }

        var allFindings = new List<Finding>();
        var rulesExecuted = 0;

        foreach (var rule in _rules)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var produced = rule.Analyze(scan, hosts);
                rulesExecuted++;
                if (produced.Count > 0)
                    allFindings.AddRange(produced);

                _logger.LogDebug(
                    "Rule {RuleId} produced {Count} finding(s) for Scan {ScanId}",
                    rule.RuleId, produced.Count, scanId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Analysis rule {RuleId} failed for Scan {ScanId}", rule.RuleId, scanId);
            }
        }

        var unique = Deduplicate(allFindings);

        int removed;
        try
        {
            var prior = await _findings.GetByScanIdAsync(scanId, cancellationToken).ConfigureAwait(false);
            removed = prior.Count(f => f.RuleId != null);
            await _findings.DeleteAnalysisFindingsByScanIdAsync(scanId, cancellationToken).ConfigureAwait(false);

            if (unique.Count > 0)
                await _findings.AddRangeAsync(unique, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist findings for Scan {ScanId}", scanId);
            return SecurityAnalysisResult.Failure("Failed to persist analysis findings.");
        }

        sw.Stop();
        _logger.LogInformation(
            "Security analysis completed for Scan {ScanId}: rules={Rules}, created={Created}, removed={Removed}, duration={Duration}ms",
            scanId, rulesExecuted, unique.Count, removed, sw.ElapsedMilliseconds);

        return SecurityAnalysisResult.Success(scanId, rulesExecuted, unique.Count, removed, sw.Elapsed);
    }

    private static List<Finding> Deduplicate(List<Finding> findings)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<Finding>();
        foreach (var f in findings)
        {
            var key = $"{f.RuleId}|{f.HostId}|{f.ServiceId}";
            if (!seen.Add(key))
                continue;
            result.Add(f);
        }
        return result;
    }
}

public sealed class FindingService : IFindingService
{
    private readonly IFindingRepository _repo;

    public FindingService(IFindingRepository repo)
    {
        _repo = repo;
    }

    public Task<IReadOnlyList<Finding>> SearchAsync(
        string? searchText,
        Severity? severityFilter,
        FindingStatus? statusFilter,
        Guid? scanId,
        CancellationToken cancellationToken = default) =>
        _repo.SearchAsync(searchText, severityFilter, statusFilter, scanId, cancellationToken);

    public Task<Finding?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _repo.GetByIdAsync(id, cancellationToken);

    public Task<int> GetTotalCountAsync(CancellationToken cancellationToken = default) =>
        _repo.CountAsync(cancellationToken);

    public Task<int> GetOpenCountAsync(CancellationToken cancellationToken = default) =>
        _repo.CountOpenAsync(cancellationToken);

    public Task<int> GetHighOrCriticalCountAsync(CancellationToken cancellationToken = default) =>
        _repo.CountHighOrCriticalAsync(cancellationToken);
}
