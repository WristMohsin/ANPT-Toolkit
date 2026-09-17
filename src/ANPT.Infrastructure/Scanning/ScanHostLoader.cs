using ANPT.Application.Services;
using ANPT.Domain.Entities;
using ANPT.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ANPT.Infrastructure.Scanning;

public sealed class ScanHostLoader : IScanHostLoader
{
    private readonly AnptDbContext _db;

    public ScanHostLoader(AnptDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Host>> GetHostsForScanAsync(Guid scanId, CancellationToken cancellationToken = default)
    {
        return await _db.Hosts
            .AsNoTracking()
            .Include(h => h.Services)
            .Include(h => h.Addresses)
            .Include(h => h.Hostnames)
            .Where(h => h.ScanId == scanId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
