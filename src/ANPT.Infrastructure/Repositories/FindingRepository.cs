using ANPT.Application.Interfaces;
using ANPT.Application.Models.Findings;
using ANPT.Domain.Entities;
using ANPT.Domain.Enums;
using ANPT.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ANPT.Infrastructure.Repositories;

public sealed class FindingRepository : IFindingRepository
{
    private readonly AnptDbContext _db;

    public FindingRepository(AnptDbContext db)
    {
        _db = db;
    }

    public async Task<Finding?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _db.Findings
            .AsNoTracking()
            .Include(f => f.Host)
            .Include(f => f.Service)
            .Include(f => f.Scan)!.ThenInclude(s => s!.Target)
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Finding>> GetByScanIdAsync(Guid scanId, CancellationToken cancellationToken = default) =>
        await _db.Findings
            .AsNoTracking()
            .Include(f => f.Host)
            .Include(f => f.Service)
            .Where(f => f.ScanId == scanId)
            .OrderByDescending(f => f.Severity)
            .ThenByDescending(f => f.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Finding>> SearchAsync(
        string? searchText,
        Severity? severityFilter,
        FindingStatus? statusFilter,
        Guid? scanId,
        string? ruleId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Findings
            .AsNoTracking()
            .Include(f => f.Host)
            .Include(f => f.Service)
            .Include(f => f.Scan)
            .AsQueryable();

        if (scanId.HasValue)
            query = query.Where(f => f.ScanId == scanId.Value);

        if (severityFilter.HasValue)
            query = query.Where(f => f.Severity == severityFilter.Value);

        if (statusFilter.HasValue)
            query = query.Where(f => f.Status == statusFilter.Value);

        if (!string.IsNullOrWhiteSpace(ruleId))
            query = query.Where(f => f.RuleId == ruleId);

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var term = searchText.Trim().ToLowerInvariant();
            query = query.Where(f =>
                f.Title.ToLower().Contains(term) ||
                f.Description.ToLower().Contains(term) ||
                (f.RuleId != null && f.RuleId.ToLower().Contains(term)) ||
                (f.Evidence != null && f.Evidence.ToLower().Contains(term)) ||
                (f.Recommendation != null && f.Recommendation.ToLower().Contains(term)) ||
                (f.AffectedPort != null && f.AffectedPort.ToLower().Contains(term)) ||
                (f.AffectedService != null && f.AffectedService.ToLower().Contains(term)) ||
                (f.Host != null && (
                    (f.Host.IpAddress != null && f.Host.IpAddress.ToLower().Contains(term)) ||
                    (f.Host.Hostname != null && f.Host.Hostname.ToLower().Contains(term)) ||
                    (f.Host.MacAddress != null && f.Host.MacAddress.ToLower().Contains(term)))) ||
                (f.Service != null && (
                    (f.Service.ServiceName != null && f.Service.ServiceName.ToLower().Contains(term)) ||
                    f.Service.Port.ToString().Contains(term))));
        }

        return await query
            .OrderByDescending(f => f.Severity)
            .ThenByDescending(f => f.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountAsync(CancellationToken cancellationToken = default) =>
        _db.Findings.CountAsync(cancellationToken);

    public Task<int> CountOpenAsync(CancellationToken cancellationToken = default) =>
        _db.Findings.CountAsync(
            f => f.Status == FindingStatus.Detected
                 || f.Status == FindingStatus.Potential
                 || f.Status == FindingStatus.Confirmed
                 || f.Status == FindingStatus.NeedsVerification,
            cancellationToken);

    public Task<int> CountHighOrCriticalAsync(CancellationToken cancellationToken = default) =>
        _db.Findings.CountAsync(
            f => f.Severity == Severity.High || f.Severity == Severity.Critical,
            cancellationToken);

    public Task<int> CountByScanAsync(Guid scanId, CancellationToken cancellationToken = default) =>
        _db.Findings.CountAsync(f => f.ScanId == scanId, cancellationToken);

    public Task<int> CountHighOrCriticalByScanAsync(Guid scanId, CancellationToken cancellationToken = default) =>
        _db.Findings.CountAsync(
            f => f.ScanId == scanId && (f.Severity == Severity.High || f.Severity == Severity.Critical),
            cancellationToken);

    public async Task<FindingSummary> GetSummaryAsync(Guid? scanId = null, CancellationToken cancellationToken = default)
    {
        var query = _db.Findings.AsNoTracking().AsQueryable();
        if (scanId.HasValue)
            query = query.Where(f => f.ScanId == scanId.Value);

        var list = await query.Select(f => new { f.Severity, f.Status, f.RuleId }).ToListAsync(cancellationToken);

        var byRule = list
            .Where(x => !string.IsNullOrEmpty(x.RuleId))
            .GroupBy(x => x.RuleId!)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        static bool IsOpen(FindingStatus s) =>
            s is FindingStatus.Detected or FindingStatus.Potential
                or FindingStatus.Confirmed or FindingStatus.NeedsVerification;

        return new FindingSummary
        {
            Total = list.Count,
            Informational = list.Count(x => x.Severity == Severity.Info),
            Low = list.Count(x => x.Severity == Severity.Low),
            Medium = list.Count(x => x.Severity == Severity.Medium),
            High = list.Count(x => x.Severity == Severity.High),
            Critical = list.Count(x => x.Severity == Severity.Critical),
            Open = list.Count(x => IsOpen(x.Status)),
            Remediated = list.Count(x => x.Status == FindingStatus.Remediated),
            FalsePositive = list.Count(x => x.Status == FindingStatus.FalsePositive),
            ByRule = byRule
        };
    }

    public async Task AddRangeAsync(IEnumerable<Finding> findings, CancellationToken cancellationToken = default)
    {
        await _db.Findings.AddRangeAsync(findings, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteByScanIdAsync(Guid scanId, CancellationToken cancellationToken = default)
    {
        var existing = await _db.Findings.Where(f => f.ScanId == scanId).ToListAsync(cancellationToken);
        if (existing.Count == 0) return;
        _db.Findings.RemoveRange(existing);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAnalysisFindingsByScanIdAsync(Guid scanId, CancellationToken cancellationToken = default)
    {
        var existing = await _db.Findings
            .Where(f => f.ScanId == scanId && f.RuleId != null)
            .ToListAsync(cancellationToken);
        if (existing.Count == 0) return;
        _db.Findings.RemoveRange(existing);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
