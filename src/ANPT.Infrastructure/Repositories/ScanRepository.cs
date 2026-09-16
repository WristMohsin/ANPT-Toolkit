using ANPT.Application.Interfaces;
using ANPT.Domain.Entities;
using ANPT.Domain.Enums;
using ANPT.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ANPT.Infrastructure.Repositories;

public class ScanRepository : IScanRepository
{
    private readonly AnptDbContext _db;

    public ScanRepository(AnptDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Scan>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _db.Scans
            .AsNoTracking()
            .Include(s => s.Target)
            .Include(s => s.ScanProfile)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Scan>> SearchAsync(
        string? searchText,
        ScanStatus? statusFilter,
        Guid? targetId,
        Guid? profileId,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Scans
            .AsNoTracking()
            .Include(s => s.Target)
            .Include(s => s.ScanProfile)
            .AsQueryable();

        if (statusFilter.HasValue)
            query = query.Where(s => s.Status == statusFilter.Value);

        if (targetId.HasValue)
            query = query.Where(s => s.TargetId == targetId.Value);

        if (profileId.HasValue)
            query = query.Where(s => s.ScanProfileId == profileId.Value);

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var term = searchText.Trim().ToLowerInvariant();
            query = query.Where(s =>
                s.Name.ToLower().Contains(term) ||
                (s.Target != null && (
                    s.Target.Name.ToLower().Contains(term) ||
                    s.Target.Address.ToLower().Contains(term))) ||
                (s.ScanProfile != null && s.ScanProfile.Name.ToLower().Contains(term)) ||
                (s.StatusMessage != null && s.StatusMessage.ToLower().Contains(term)));
        }

        return await query
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<Scan?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _db.Scans
            .Include(s => s.Target)
            .Include(s => s.ScanProfile)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<int> CountAsync(CancellationToken cancellationToken = default) =>
        await _db.Scans.CountAsync(cancellationToken);

    public async Task<int> CountActiveAsync(CancellationToken cancellationToken = default) =>
        await _db.Scans.CountAsync(
            s => s.Status == ScanStatus.Queued || s.Status == ScanStatus.Running,
            cancellationToken);

    public async Task<Scan> AddAsync(Scan scan, CancellationToken cancellationToken = default)
    {
        _db.Scans.Add(scan);
        await _db.SaveChangesAsync(cancellationToken);
        return scan;
    }

    public async Task UpdateAsync(Scan scan, CancellationToken cancellationToken = default)
    {
        var entry = _db.Entry(scan);
        if (entry.State == EntityState.Detached)
            _db.Scans.Update(scan);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
