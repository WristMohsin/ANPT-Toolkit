using ANPT.Application.Interfaces;
using ANPT.Domain.Entities;
using ANPT.Domain.Enums;
using ANPT.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ANPT.Infrastructure.Repositories;

public class FindingRepository : IFindingRepository
{
    private readonly AnptDbContext _db;

    public FindingRepository(AnptDbContext db)
    {
        _db = db;
    }

    public async Task<Finding?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _db.Findings
            .AsNoTracking()
            .Include(f => f.Scan)
            .Include(f => f.Host)
            .Include(f => f.Service)
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Finding>> GetByScanIdAsync(Guid scanId, CancellationToken cancellationToken = default)
    {
        return await _db.Findings
            .AsNoTracking()
            .Include(f => f.Host)
            .Include(f => f.Service)
            .Where(f => f.ScanId == scanId)
            .OrderByDescending(f => f.Severity)
            .ThenBy(f => f.Title)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Finding>> SearchAsync(
        string? searchText,
        Severity? severityFilter,
        FindingStatus? statusFilter,
        Guid? scanId,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Findings
            .AsNoTracking()
            .Include(f => f.Scan)
            .Include(f => f.Host)
            .Include(f => f.Service)
            .AsQueryable();

        if (severityFilter.HasValue)
            query = query.Where(f => f.Severity == severityFilter.Value);

        if (statusFilter.HasValue)
            query = query.Where(f => f.Status == statusFilter.Value);

        if (scanId.HasValue)
            query = query.Where(f => f.ScanId == scanId.Value);

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var term = searchText.Trim().ToLowerInvariant();
            query = query.Where(f =>
                f.Title.ToLower().Contains(term) ||
                f.Description.ToLower().Contains(term) ||
                (f.RuleId != null && f.RuleId.ToLower().Contains(term)) ||
                (f.AffectedPort != null && f.AffectedPort.ToLower().Contains(term)) ||
                (f.AffectedService != null && f.AffectedService.ToLower().Contains(term)) ||
                (f.Host != null && (
                    f.Host.IpAddress.ToLower().Contains(term) ||
                    (f.Host.Hostname != null && f.Host.Hostname.ToLower().Contains(term)))));
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
