using ANPT.Application.Interfaces;
using ANPT.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace ANPT.Application.Services;

public class ScanExecutionService : IScanExecutionService
{
    private readonly IScanRepository _scans;
    private readonly IScanProfileRepository _profiles;
    private readonly ITargetService _targets;
    private readonly INmapProcessRunner _nmap;
    private readonly ILogger<ScanExecutionService> _logger;

    public ScanExecutionService(
        IScanRepository scans,
        IScanProfileRepository profiles,
        ITargetService targets,
        INmapProcessRunner nmap,
        ILogger<ScanExecutionService> logger)
    {
        _scans = scans;
        _profiles = profiles;
        _targets = targets;
        _nmap = nmap;
        _logger = logger;
    }

    public Task<NmapAvailabilityResult> CheckNmapAvailabilityAsync(CancellationToken cancellationToken = default) =>
        _nmap.CheckAvailabilityAsync(cancellationToken);

    public async Task<ScanExecutionResult> StartAsync(Guid scanId, CancellationToken cancellationToken = default)
    {
        if (scanId == Guid.Empty)
            return ScanExecutionResult.Failure("A scan id is required.");

        var scan = await _scans.GetByIdAsync(scanId, cancellationToken);
        if (scan is null)
            return ScanExecutionResult.Failure("Scan not found.");

        if (scan.Status != ScanStatus.Queued)
            return ScanExecutionResult.Failure($"Cannot start a scan with status {scan.Status}. Only Queued scans can be started.");

        var target = await _targets.GetByIdAsync(scan.TargetId, cancellationToken);
        if (target is null)
            return await FailScanAsync(scan, "Target no longer exists. Scan cannot start.", cancellationToken);

        if (target.Status == TargetStatus.Archived)
            return await FailScanAsync(scan, "Target is archived. Scan cannot start.", cancellationToken);

        if (target.Status == TargetStatus.Disabled)
            return await FailScanAsync(scan, "Target is disabled. Scan cannot start.", cancellationToken);

        if (!_targets.IsEligibleForScan(target))
            return await FailScanAsync(
                scan,
                "Target is not eligible for scanning. Confirm written authorization and ensure the target is Active.",
                cancellationToken);

        var profile = await _profiles.GetByIdAsync(scan.ScanProfileId, cancellationToken);
        if (profile is null)
            return await FailScanAsync(scan, "Scan profile no longer exists.", cancellationToken);

        if (!profile.IsEnabled)
            return await FailScanAsync(scan, "Scan profile is disabled.", cancellationToken);

        var availability = await _nmap.CheckAvailabilityAsync(cancellationToken);
        if (!availability.IsAvailable || string.IsNullOrWhiteSpace(availability.ExecutablePath))
            return await FailScanAsync(
                scan,
                availability.ErrorMessage ?? "Nmap is not available on this system.",
                cancellationToken);

        if (!NmapArgumentBuilder.TryBuild(target.Address, profile, xmlOutputPath: null, out var arguments, out var argError))
            return await FailScanAsync(scan, argError ?? "Unable to build safe scan arguments.", cancellationToken);

        _logger.LogInformation(
            "Scan execution requested: ScanId={ScanId} TargetId={TargetId} Profile={Profile}",
            scan.Id, target.Id, profile.Name);

        scan.Status = ScanStatus.Running;
        scan.CurrentStage = ScanStage.Discovery;
        scan.StartedAt = DateTime.UtcNow;
        scan.StatusMessage = "Starting Nmap process…";
        scan.ProgressPercent = 1;
        scan.UpdatedAt = DateTime.UtcNow;
        scan.ErrorMessage = null;

        try
        {
            await _scans.UpdateAsync(scan, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark scan {ScanId} as Running", scan.Id);
            return ScanExecutionResult.Failure("Unable to update scan status before execution.");
        }

        var request = new NmapRunRequest
        {
            ExecutablePath = availability.ExecutablePath!,
            Arguments = arguments,
            XmlOutputPath = null,
            WorkingDirectory = null
        };

        NmapRunResult processResult;
        try
        {
            processResult = await _nmap.RunAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Nmap runner threw for scan {ScanId}", scan.Id);
            await FailScanAsync(scan, "Scan process failed unexpectedly.", CancellationToken.None);
            return ScanExecutionResult.Failure("Scan process failed unexpectedly.");
        }

        if (!processResult.Started)
        {
            await FailScanAsync(scan, processResult.ErrorMessage ?? "Failed to start Nmap process.", CancellationToken.None);
            return ScanExecutionResult.Failure(processResult.ErrorMessage ?? "Failed to start Nmap process.");
        }

        scan.CompletedAt = DateTime.UtcNow;
        scan.UpdatedAt = DateTime.UtcNow;
        scan.ProgressPercent = 100;

        if (processResult.Cancelled)
        {
            scan.Status = ScanStatus.Cancelled;
            scan.CurrentStage = ScanStage.Completed;
            scan.StatusMessage = "Cancelled during execution.";
            _logger.LogInformation("Scan {ScanId} cancelled during execution", scan.Id);
        }
        else if (processResult.Failed || processResult.ExitCode is not 0 and not null)
        {
            scan.Status = ScanStatus.Failed;
            scan.CurrentStage = ScanStage.Completed;
            scan.StatusMessage = processResult.ErrorMessage ?? "Nmap finished with a non-zero exit code.";
            scan.ErrorMessage = Truncate(processResult.StandardError, 2000)
                ?? processResult.ErrorMessage;
            _logger.LogWarning(
                "Scan {ScanId} failed. ExitCode={ExitCode}",
                scan.Id, processResult.ExitCode);
        }
        else
        {
            scan.Status = ScanStatus.Completed;
            scan.CurrentStage = ScanStage.Completed;
            scan.StatusMessage =
                "Nmap process completed. Host/service/finding parsing is not implemented in Phase 5A.";
            _logger.LogInformation(
                "Scan {ScanId} process completed. ExitCode={ExitCode} Duration={Duration}",
                scan.Id, processResult.ExitCode, processResult.Duration);
        }

        try
        {
            await _scans.UpdateAsync(scan, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist final status for scan {ScanId}", scan.Id);
            return ScanExecutionResult.Failure("Process finished but status could not be saved.");
        }

        return ScanExecutionResult.Success(scan.Id, processResult);
    }

    private async Task<ScanExecutionResult> FailScanAsync(
        Domain.Entities.Scan scan,
        string message,
        CancellationToken cancellationToken)
    {
        _logger.LogWarning("Scan {ScanId} cannot execute: {Reason}", scan.Id, message);

        scan.Status = ScanStatus.Failed;
        scan.CompletedAt = DateTime.UtcNow;
        scan.UpdatedAt = DateTime.UtcNow;
        scan.StatusMessage = message;
        scan.ErrorMessage = Truncate(message, 2000);
        scan.CurrentStage = ScanStage.Completed;
        scan.ProgressPercent = 0;

        try
        {
            await _scans.UpdateAsync(scan, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark scan {ScanId} as Failed", scan.Id);
        }

        return ScanExecutionResult.Failure(message);
    }

    private static string? Truncate(string? value, int max) =>
        string.IsNullOrEmpty(value) ? value : value.Length <= max ? value : value[..max];
}
