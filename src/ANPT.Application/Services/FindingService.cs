using ANPT.Application.Interfaces;
using ANPT.Application.Models.Findings;
using ANPT.Domain.Entities;
using ANPT.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace ANPT.Application.Services;

public sealed class FindingService : IFindingService
{
    private readonly IFindingRepository _repo;
    private readonly IScanRepository _scans;
    private readonly IScanHostLoader _hostLoader;
    private readonly IEnumerable<IAnalysisRule> _rules;
    private readonly ILogger<FindingService> _logger;

    public FindingService(
        IFindingRepository repo,
        IScanRepository scans,
        IScanHostLoader hostLoader,
        IEnumerable<IAnalysisRule> rules,
        ILogger<FindingService> logger)
    {
        _repo = repo;
        _scans = scans;
        _hostLoader = hostLoader;
        _rules = rules;
        _logger = logger;
    }

    public Task<IReadOnlyList<Finding>> SearchAsync(
        string? searchText,
        Severity? severityFilter,
        FindingStatus? statusFilter,
        Guid? scanId,
        string? ruleId = null,
        CancellationToken cancellationToken = default) =>
        _repo.SearchAsync(searchText, severityFilter, statusFilter, scanId, ruleId, cancellationToken);

    public Task<Finding?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _repo.GetByIdAsync(id, cancellationToken);

    public async Task<FindingDetailDto?> GetDetailAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var f = await _repo.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (f is null) return null;
        return MapDetail(f);
    }

    public Task<int> GetTotalCountAsync(CancellationToken cancellationToken = default) =>
        _repo.CountAsync(cancellationToken);

    public Task<int> GetOpenCountAsync(CancellationToken cancellationToken = default) =>
        _repo.CountOpenAsync(cancellationToken);

    public Task<int> GetHighOrCriticalCountAsync(CancellationToken cancellationToken = default) =>
        _repo.CountHighOrCriticalAsync(cancellationToken);

    public Task<FindingSummary> GetSummaryAsync(Guid? scanId = null, CancellationToken cancellationToken = default) =>
        _repo.GetSummaryAsync(scanId, cancellationToken);

    public Task<int> GetCountByScanAsync(Guid scanId, CancellationToken cancellationToken = default) =>
        _repo.CountByScanAsync(scanId, cancellationToken);

    public Task<int> GetHighOrCriticalCountByScanAsync(Guid scanId, CancellationToken cancellationToken = default) =>
        _repo.CountHighOrCriticalByScanAsync(scanId, cancellationToken);

    public async Task<AssessmentReport?> BuildAssessmentReportAsync(
        Guid scanId,
        CancellationToken cancellationToken = default)
    {
        var scan = await _scans.GetByIdAsync(scanId, cancellationToken).ConfigureAwait(false);
        if (scan is null) return null;

        var findings = await _repo.GetByScanIdAsync(scanId, cancellationToken).ConfigureAwait(false);
        var summary = await _repo.GetSummaryAsync(scanId, cancellationToken).ConfigureAwait(false);

        int hostCount = 0, serviceCount = 0;
        try
        {
            var hosts = await _hostLoader.GetHostsForScanAsync(scanId, cancellationToken).ConfigureAwait(false);
            hostCount = hosts.Count;
            serviceCount = hosts.Sum(h => h.Services?.Count ?? 0);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load hosts for assessment report Scan {ScanId}", scanId);
        }

        var details = findings.Select(MapDetail).ToList();
        var topRecs = details
            .Where(d => !string.IsNullOrWhiteSpace(d.Recommendation))
            .Select(d => d.Recommendation!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToList();

        return new AssessmentReport
        {
            ScanId = scan.Id,
            ScanName = scan.Name,
            TargetName = scan.Target?.Name,
            TargetAddress = scan.Target?.Address,
            ScanStartedAt = scan.StartedAt,
            ScanCompletedAt = scan.CompletedAt,
            ScanStatus = scan.Status.ToString(),
            ProfileName = scan.ScanProfile?.Name,
            Summary = summary,
            HostCount = hostCount,
            ServiceCount = serviceCount,
            Findings = details,
            TopRecommendations = topRecs
        };
    }

    private FindingDetailDto MapDetail(Finding f)
    {
        var risk = FindingRiskContext.FromSeverity(f.Severity, f.RuleId);
        var category = _rules
            .FirstOrDefault(r => string.Equals(r.RuleId, f.RuleId, StringComparison.OrdinalIgnoreCase))
            ?.Category;

        string hostDisplay = "-";
        if (f.Host is not null)
        {
            hostDisplay = !string.IsNullOrWhiteSpace(f.Host.IpAddress)
                ? f.Host.IpAddress
                : f.Host.Hostname ?? f.Host.MacAddress ?? "-";
        }

        return new FindingDetailDto
        {
            Id = f.Id,
            Title = f.Title,
            Description = f.Description,
            Severity = f.Severity,
            Status = f.Status,
            Priority = risk.Priority,
            RiskLabel = risk.RiskLabel,
            RiskRationale = risk.Rationale,
            RuleId = f.RuleId,
            Category = category,
            Evidence = f.Evidence,
            Impact = f.Impact,
            Recommendation = f.Recommendation,
            Reference = f.Reference,
            Confidence = f.Confidence,
            CreatedAt = f.CreatedAt,
            HostId = f.HostId,
            HostDisplay = hostDisplay,
            HostIp = f.Host?.IpAddress,
            HostHostname = f.Host?.Hostname,
            HostMac = f.Host?.MacAddress,
            ServiceId = f.ServiceId,
            Protocol = f.Service?.Protocol,
            Port = f.Service?.Port,
            ServiceName = f.Service?.ServiceName,
            Product = f.Service?.Product,
            Version = f.Service?.Version,
            PortState = f.Service?.State,
            AffectedPort = f.AffectedPort,
            AffectedService = f.AffectedService,
            ScanId = f.ScanId,
            ScanName = f.Scan?.Name ?? "-",
            TargetName = f.Scan?.Target?.Name,
            TargetAddress = f.Scan?.Target?.Address,
            ScanCompletedAt = f.Scan?.CompletedAt
        };
    }
}
