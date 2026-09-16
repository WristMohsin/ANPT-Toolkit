using ANPT.Application.Interfaces;
using ANPT.Domain.Entities;
using ANPT.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace ANPT.Application.Services;

public class ScanService : IScanService
{
    private readonly IScanRepository _scans;
    private readonly IScanProfileRepository _profiles;
    private readonly ITargetService _targets;
    private readonly ILogger<ScanService> _logger;

    public ScanService(
        IScanRepository scans,
        IScanProfileRepository profiles,
        ITargetService targets,
        ILogger<ScanService> logger)
    {
        _scans = scans;
        _profiles = profiles;
        _targets = targets;
        _logger = logger;
    }

    public Task<IReadOnlyList<Scan>> GetAllAsync(CancellationToken cancellationToken = default) =>
        _scans.GetAllAsync(cancellationToken);

    public Task<IReadOnlyList<Scan>> SearchAsync(
        string? searchText,
        ScanStatus? statusFilter,
        Guid? targetId,
        Guid? profileId,
        CancellationToken cancellationToken = default) =>
        _scans.SearchAsync(searchText, statusFilter, targetId, profileId, cancellationToken);

    public Task<Scan?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _scans.GetByIdAsync(id, cancellationToken);

    public Task<int> GetTotalCountAsync(CancellationToken cancellationToken = default) =>
        _scans.CountAsync(cancellationToken);

    public Task<int> GetActiveCountAsync(CancellationToken cancellationToken = default) =>
        _scans.CountActiveAsync(cancellationToken);

    public Task<IReadOnlyList<ScanProfile>> GetEnabledProfilesAsync(CancellationToken cancellationToken = default) =>
        _profiles.GetEnabledAsync(cancellationToken);

    public async Task<IReadOnlyList<Target>> GetEligibleTargetsAsync(CancellationToken cancellationToken = default)
    {
        var all = await _targets.GetAllAsync(cancellationToken);
        return all.Where(_targets.IsEligibleForScan).ToList();
    }

    public async Task<ScanServiceResult> CreateAsync(CreateScanRequest request, string? createdBy, CancellationToken cancellationToken = default)
    {
        if (request.TargetId == Guid.Empty)
            return ScanServiceResult.Failure("A target is required.");
        if (request.ScanProfileId == Guid.Empty)
            return ScanServiceResult.Failure("A scan profile is required.");

        var target = await _targets.GetByIdAsync(request.TargetId, cancellationToken);
        if (target is null)
            return ScanServiceResult.Failure("Target not found.");

        if (target.Status == TargetStatus.Archived)
            return ScanServiceResult.Failure("Archived targets cannot be scanned.");
        if (target.Status == TargetStatus.Disabled)
            return ScanServiceResult.Failure("Disabled targets cannot be scanned.");
        if (!_targets.IsEligibleForScan(target))
            return ScanServiceResult.Failure("Target is not eligible for scanning. Confirm written authorization and ensure the target is Active.");

        var profile = await _profiles.GetByIdAsync(request.ScanProfileId, cancellationToken);
        if (profile is null)
            return ScanServiceResult.Failure("Scan profile not found.");
        if (!profile.IsEnabled)
            return ScanServiceResult.Failure("Selected scan profile is disabled.");

        var name = string.IsNullOrWhiteSpace(request.Name)
            ? $"{target.Name} — {profile.Name}"
            : request.Name.Trim();
        if (name.Length > 200)
            name = name[..200];

        var scan = new Scan
        {
            TargetId = target.Id,
            ScanProfileId = profile.Id,
            Name = name,
            Status = ScanStatus.Queued,
            CurrentStage = ScanStage.Initializing,
            ProgressPercent = 0,
            StatusMessage = "Queued — awaiting scan engine (not implemented in this phase).",
            CreatedBy = string.IsNullOrWhiteSpace(createdBy)
                ? null
                : (createdBy.Trim().Length > 100 ? createdBy.Trim()[..100] : createdBy.Trim())
        };

        try
        {
            var created = await _scans.AddAsync(scan, cancellationToken);
            _logger.LogInformation(
                "Scan created: {ScanId} Target={Target} Profile={Profile} By={User}",
                created.Id, target.Name, profile.Name, createdBy);
            var full = await _scans.GetByIdAsync(created.Id, cancellationToken) ?? created;
            return ScanServiceResult.Success(full);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create scan for target {TargetId}", request.TargetId);
            return ScanServiceResult.Failure("Unable to create scan. Please try again.");
        }
    }

    public async Task<ScanServiceResult> CancelAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var existing = await _scans.GetByIdAsync(id, cancellationToken);
        if (existing is null)
            return ScanServiceResult.Failure("Scan not found.");

        if (existing.Status is ScanStatus.Completed or ScanStatus.Failed or ScanStatus.Cancelled)
            return ScanServiceResult.Failure($"Cannot cancel a scan with status {existing.Status}.");

        if (existing.Status is not (ScanStatus.Queued or ScanStatus.Running))
            return ScanServiceResult.Failure("Only queued or running scans can be cancelled.");

        existing.Status = ScanStatus.Cancelled;
        existing.CompletedAt = DateTime.UtcNow;
        existing.StatusMessage = "Cancelled by operator.";
        existing.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _scans.UpdateAsync(existing, cancellationToken);
            _logger.LogInformation("Scan cancelled: {ScanId}", existing.Id);
            return ScanServiceResult.Success(existing);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cancel scan {ScanId}", id);
            return ScanServiceResult.Failure("Unable to cancel scan. Please try again.");
        }
    }
}
