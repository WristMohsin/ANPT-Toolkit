using ANPT.Domain.Entities;
using ANPT.Domain.Enums;

namespace ANPT.Application.Interfaces;

public interface ITargetService
{
    Task<IReadOnlyList<Target>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Target>> SearchAsync(string? searchText, TargetStatus? statusFilter, CancellationToken cancellationToken = default);
    Task<Target?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<int> GetTotalCountAsync(CancellationToken cancellationToken = default);
    Task<TargetServiceResult> CreateAsync(CreateTargetRequest request, string? createdBy, CancellationToken cancellationToken = default);
    Task<TargetServiceResult> UpdateAsync(UpdateTargetRequest request, CancellationToken cancellationToken = default);
    Task<TargetServiceResult> ArchiveAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TargetServiceResult> SetAuthorizationAsync(Guid id, bool confirmed, CancellationToken cancellationToken = default);
    bool IsEligibleForScan(Target target);
}

public sealed class CreateTargetRequest
{
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string TargetType { get; set; } = "Host";
    public string? Description { get; set; }
    public bool AuthorizationConfirmed { get; set; }
}

public sealed class UpdateTargetRequest
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string TargetType { get; set; } = "Host";
    public string? Description { get; set; }
    public bool AuthorizationConfirmed { get; set; }
    public TargetStatus Status { get; set; } = TargetStatus.Active;
}

public sealed class TargetServiceResult
{
    public bool Succeeded { get; private init; }
    public string? ErrorMessage { get; private init; }
    public Target? Target { get; private init; }

    public static TargetServiceResult Success(Target target) =>
        new() { Succeeded = true, Target = target };

    public static TargetServiceResult Failure(string message) =>
        new() { Succeeded = false, ErrorMessage = message };
}
