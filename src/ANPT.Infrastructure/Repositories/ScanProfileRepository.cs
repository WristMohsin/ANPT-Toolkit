using ANPT.Application.Interfaces;
using ANPT.Domain.Entities;
using ANPT.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ANPT.Infrastructure.Repositories;

public class ScanProfileRepository : IScanProfileRepository
{
    private readonly AnptDbContext _db;

    public ScanProfileRepository(AnptDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ScanProfile>> GetEnabledAsync(CancellationToken cancellationToken = default)
    {
        return await _db.ScanProfiles
            .AsNoTracking()
            .Where(p => p.IsEnabled)
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<ScanProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _db.ScanProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }
}
