using ANPT.Domain.Entities;
using ANPT.Domain.Enums;
using ANPT.Application.Models.Findings;

namespace ANPT.Application.Interfaces;

public interface IFindingService
{
    Task<IReadOnlyList<Finding>> SearchAsync(
        string? searchText,
        Severity? severityFilter,
        FindingStatus? statusFilter,
        Guid? scanId,
        string? ruleId = null,
        CancellationToken cancellationToken = default);

    Task<Finding?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<FindingDetailDto?> GetDetailAsync(Guid id, CancellationToken cancellationToken = default);

    Task<int> GetTotalCountAsync(CancellationToken cancellationToken = default);

    Task<int> GetOpenCountAsync(CancellationToken cancellationToken = default);

    Task<int> GetHighOrCriticalCountAsync(CancellationToken cancellationToken = default);

    Task<FindingSummary> GetSummaryAsync(Guid? scanId = null, CancellationToken cancellationToken = default);

    Task<AssessmentReport?> BuildAssessmentReportAsync(Guid scanId, CancellationToken cancellationToken = default);

    Task<int> GetCountByScanAsync(Guid scanId, CancellationToken cancellationToken = default);

    Task<int> GetHighOrCriticalCountByScanAsync(Guid scanId, CancellationToken cancellationToken = default);
}
