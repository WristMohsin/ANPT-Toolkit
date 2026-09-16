using ANPT.Application.Interfaces;
using ANPT.Domain.Entities;
using ANPT.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace ANPT.Application.Services;

public class TargetService : ITargetService
{
    private readonly ITargetRepository _repository;
    private readonly ILogger<TargetService> _logger;

    private static readonly HashSet<string> AllowedTargetTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Host", "Network", "Range", "URL", "Domain"
    };

    public TargetService(ITargetRepository repository, ILogger<TargetService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public Task<IReadOnlyList<Target>> GetAllAsync(CancellationToken cancellationToken = default) =>
        _repository.GetAllAsync(cancellationToken);

    public Task<IReadOnlyList<Target>> SearchAsync(string? searchText, TargetStatus? statusFilter, CancellationToken cancellationToken = default) =>
        _repository.SearchAsync(searchText, statusFilter, cancellationToken);

    public Task<Target?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _repository.GetByIdAsync(id, cancellationToken);

    public Task<int> GetTotalCountAsync(CancellationToken cancellationToken = default) =>
        _repository.CountAsync(cancellationToken);

    public async Task<TargetServiceResult> CreateAsync(CreateTargetRequest request, string? createdBy, CancellationToken cancellationToken = default)
    {
        var validation = ValidateCore(request.Name, request.Address, request.TargetType, request.Description);
        if (validation is not null)
            return TargetServiceResult.Failure(validation);

        var target = new Target
        {
            Name = request.Name.Trim(),
            Address = request.Address.Trim(),
            TargetType = NormalizeTargetType(request.TargetType),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            AuthorizationConfirmed = request.AuthorizationConfirmed,
            Status = TargetStatus.Active,
            CreatedBy = string.IsNullOrWhiteSpace(createdBy) ? null : createdBy.Trim()
        };

        try
        {
            var created = await _repository.AddAsync(target, cancellationToken);
            _logger.LogInformation(
                "Target created: {Name} ({Address}), AuthConfirmed={Auth}, By={User}",
                created.Name, created.Address, created.AuthorizationConfirmed, createdBy);
            return TargetServiceResult.Success(created);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create target {Name}", request.Name);
            return TargetServiceResult.Failure("Unable to create target. Please try again.");
        }
    }

    public async Task<TargetServiceResult> UpdateAsync(UpdateTargetRequest request, CancellationToken cancellationToken = default)
    {
        var validation = ValidateCore(request.Name, request.Address, request.TargetType, request.Description);
        if (validation is not null)
            return TargetServiceResult.Failure(validation);

        var existing = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (existing is null)
            return TargetServiceResult.Failure("Target not found.");

        existing.Name = request.Name.Trim();
        existing.Address = request.Address.Trim();
        existing.TargetType = NormalizeTargetType(request.TargetType);
        existing.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        existing.AuthorizationConfirmed = request.AuthorizationConfirmed;
        existing.Status = request.Status;
        existing.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _repository.UpdateAsync(existing, cancellationToken);
            _logger.LogInformation("Target updated: {Id} {Name}", existing.Id, existing.Name);
            return TargetServiceResult.Success(existing);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update target {Id}", request.Id);
            return TargetServiceResult.Failure("Unable to update target. Please try again.");
        }
    }

    public async Task<TargetServiceResult> ArchiveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetByIdAsync(id, cancellationToken);
        if (existing is null)
            return TargetServiceResult.Failure("Target not found.");

        if (existing.Status == TargetStatus.Archived)
            return TargetServiceResult.Success(existing);

        existing.Status = TargetStatus.Archived;
        existing.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _repository.UpdateAsync(existing, cancellationToken);
            _logger.LogInformation("Target archived: {Id} {Name}", existing.Id, existing.Name);
            return TargetServiceResult.Success(existing);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to archive target {Id}", id);
            return TargetServiceResult.Failure("Unable to archive target. Please try again.");
        }
    }

    public async Task<TargetServiceResult> SetAuthorizationAsync(Guid id, bool confirmed, CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetByIdAsync(id, cancellationToken);
        if (existing is null)
            return TargetServiceResult.Failure("Target not found.");

        existing.AuthorizationConfirmed = confirmed;
        existing.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _repository.UpdateAsync(existing, cancellationToken);
            _logger.LogInformation("Target authorization set to {Confirmed} for {Id} {Name}", confirmed, existing.Id, existing.Name);
            return TargetServiceResult.Success(existing);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set authorization for target {Id}", id);
            return TargetServiceResult.Failure("Unable to update authorization status.");
        }
    }

    /// <summary>
    /// A target is eligible for future scanning only when it is Active and the operator has explicitly confirmed authorization.
    /// Phase 3 does not implement scanning; this is the service-level boundary for later phases.
    /// </summary>
    public bool IsEligibleForScan(Target target)
    {
        if (target is null)
            return false;
        return target.Status == TargetStatus.Active && target.AuthorizationConfirmed;
    }

    private static string? ValidateCore(string name, string address, string targetType, string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "Target name is required.";
        if (name.Trim().Length > 200)
            return "Target name must be 200 characters or fewer.";

        if (string.IsNullOrWhiteSpace(address))
            return "Target address / value is required.";
        if (address.Trim().Length > 100)
            return "Target address must be 100 characters or fewer.";

        if (!string.IsNullOrWhiteSpace(targetType) && !AllowedTargetTypes.Contains(targetType.Trim()))
            return $"Target type must be one of: {string.Join(", ", AllowedTargetTypes)}.";

        if (description is not null && description.Length > 1000)
            return "Description must be 1000 characters or fewer.";

        return null;
    }

    private static string NormalizeTargetType(string? targetType)
    {
        if (string.IsNullOrWhiteSpace(targetType))
            return "Host";
        var trimmed = targetType.Trim();
        foreach (var allowed in AllowedTargetTypes)
        {
            if (string.Equals(allowed, trimmed, StringComparison.OrdinalIgnoreCase))
                return allowed;
        }
        return "Host";
    }
}
