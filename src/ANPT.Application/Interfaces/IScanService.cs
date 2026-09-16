using ANPT.Domain.Entities;
using ANPT.Domain.Enums;

namespace ANPT.Application.Interfaces;

public interface IScanService
{
    Task<IReadOnlyList<Scan>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Scan>> SearchAsync(
        string? searchText,
        ScanStatus? statusFilter,
        Guid? targetId,
        Guid? profileId,
        CancellationToken cancellationToken = default);
    Task<Scan?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<int> GetTotalCountAsync(CancellationToken cancellationToken = default);
    Task<int> GetActiveCountAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ScanProfile>> GetEnabledProfilesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Target>> GetEligibleTargetsAsync(CancellationToken cancellationToken = default);
    Task<ScanServiceResult> CreateAsync(CreateScanRequest request, string? createdBy, CancellationToken cancellationToken = default);
    Task<ScanServiceResult> CancelAsync(Guid id, CancellationToken cancellationToken = default);
}

public sealed class CreateScanRequest
{
    public Guid TargetId { get; set; }
    public Guid ScanProfileId { get; set; }
    public string? Name { get; set; }
}

public sealed class ScanServiceResult
{
    public bool Succeeded { get; private init; }
    public string? ErrorMessage { get; private init; }
    public Scan? Scan { get; private init; }

    public static ScanServiceResult Success(Scan scan) =>
        new() { Succeeded = true, Scan = scan };

    public static ScanServiceResult Failure(string message) =>
        new() { Succeeded = false, ErrorMessage = message };
}
