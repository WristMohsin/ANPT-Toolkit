using ANPT.Domain.Entities;
using ANPT.Domain.Enums;

namespace ANPT.Application.Interfaces;

public interface IScanRepository
{
    Task<IReadOnlyList<Scan>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Scan>> SearchAsync(
        string? searchText,
        ScanStatus? statusFilter,
        Guid? targetId,
        Guid? profileId,
        CancellationToken cancellationToken = default);
    Task<Scan?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<int> CountAsync(CancellationToken cancellationToken = default);
    Task<int> CountActiveAsync(CancellationToken cancellationToken = default);
    Task<Scan> AddAsync(Scan scan, CancellationToken cancellationToken = default);
    Task UpdateAsync(Scan scan, CancellationToken cancellationToken = default);
}
