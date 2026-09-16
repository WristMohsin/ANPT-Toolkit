using ANPT.Domain.Entities;
using ANPT.Domain.Enums;

namespace ANPT.Application.Interfaces;

public interface ITargetRepository
{
    Task<IReadOnlyList<Target>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Target>> SearchAsync(string? searchText, TargetStatus? statusFilter, CancellationToken cancellationToken = default);
    Task<Target?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<int> CountAsync(CancellationToken cancellationToken = default);
    Task<int> CountActiveAsync(CancellationToken cancellationToken = default);
    Task<Target> AddAsync(Target target, CancellationToken cancellationToken = default);
    Task UpdateAsync(Target target, CancellationToken cancellationToken = default);
}
