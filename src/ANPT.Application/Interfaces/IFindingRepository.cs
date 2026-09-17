using ANPT.Domain.Entities;
using ANPT.Domain.Enums;
using ANPT.Application.Models.Findings;

namespace ANPT.Application.Interfaces;

public interface IFindingRepository
{
    Task<Finding?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Finding>> GetByScanIdAsync(Guid scanId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Finding>> SearchAsync(
        string? searchText,
        Severity? severityFilter,
        FindingStatus? statusFilter,
        Guid? scanId,
        string? ruleId = null,
        CancellationToken cancellationToken = default);

    Task<int> CountAsync(CancellationToken cancellationToken = default);

    Task<int> CountOpenAsync(CancellationToken cancellationToken = default);

    Task<int> CountHighOrCriticalAsync(CancellationToken cancellationToken = default);

    Task<int> CountByScanAsync(Guid scanId, CancellationToken cancellationToken = default);

    Task<int> CountHighOrCriticalByScanAsync(Guid scanId, CancellationToken cancellationToken = default);

    Task<FindingSummary> GetSummaryAsync(Guid? scanId = null, CancellationToken cancellationToken = default);

    Task AddRangeAsync(IEnumerable<Finding> findings, CancellationToken cancellationToken = default);

    Task DeleteByScanIdAsync(Guid scanId, CancellationToken cancellationToken = default);

    Task DeleteAnalysisFindingsByScanIdAsync(Guid scanId, CancellationToken cancellationToken = default);
}
