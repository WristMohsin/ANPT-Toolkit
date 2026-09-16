using ANPT.Application.Interfaces;
using ANPT.Domain.Entities;
using ANPT.Domain.Enums;
using ANPT.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ANPT.Infrastructure.Repositories;

public class TargetRepository : ITargetRepository
{
    private readonly AnptDbContext _db;

    public TargetRepository(AnptDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Target>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _db.Targets
            .AsNoTracking()
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Target>> SearchAsync(string? searchText, TargetStatus? statusFilter, CancellationToken cancellationToken = default)
    {
        var query = _db.Targets.AsNoTracking().AsQueryable();

        if (statusFilter.HasValue)
            query = query.Where(t => t.Status == statusFilter.Value);

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var term = searchText.Trim().ToLowerInvariant();
            query = query.Where(t =>
                t.Name.ToLower().Contains(term) ||
                t.Address.ToLower().Contains(term) ||
                (t.Description != null && t.Description.ToLower().Contains(term)) ||
                t.TargetType.ToLower().Contains(term));
        }

        return await query
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<Target?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Tracking required for subsequent updates when service loads then mutates
        return await _db.Targets.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        return await _db.Targets.CountAsync(cancellationToken);
    }

    public async Task<int> CountActiveAsync(CancellationToken cancellationToken = default)
    {
        return await _db.Targets.CountAsync(t => t.Status == TargetStatus.Active, cancellationToken);
    }

    public async Task<Target> AddAsync(Target target, CancellationToken cancellationToken = default)
    {
        _db.Targets.Add(target);
        await _db.SaveChangesAsync(cancellationToken);
        return target;
    }

    public async Task UpdateAsync(Target target, CancellationToken cancellationToken = default)
    {
        var entry = _db.Entry(target);
        if (entry.State == EntityState.Detached)
        {
            _db.Targets.Update(target);
        }
        await _db.SaveChangesAsync(cancellationToken);
    }
}
