using ANPT.Domain.Entities;

namespace ANPT.Application.Interfaces;

public interface IScanProfileRepository
{
    Task<IReadOnlyList<ScanProfile>> GetEnabledAsync(CancellationToken cancellationToken = default);
    Task<ScanProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
