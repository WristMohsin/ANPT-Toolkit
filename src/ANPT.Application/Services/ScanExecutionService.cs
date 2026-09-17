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
    private readonly IScanProcessTracker _tracker;
    private readonly IScanOutputPathService _outputPaths;
    private readonly INmapXmlResultReader _xmlReader;
    private readonly INmapResultPersistenceService _persistence;
    private readonly ILogger<ScanExecutionService> _logger;

    public ScanExecutionService(
        IScanRepository scans,
        IScanProfileRepository profiles,
        ITargetService targets,
        INmapProcessRunner nmap,
        IScanProcessTracker tracker,
        IScanOutputPathService outputPaths,
        INmapXmlResultReader xmlReader,
        INmapResultPersistenceService persistence,
        ILogger<ScanExecutionService> logger)
    {
        _scans = scans;
        _profiles = profiles;
        _targets = targets;
        _nmap = nmap;
        _tracker = tracker;
        _outputPaths = outputPaths;
        _xmlReader = xmlReader;
        _persistence = persistence;
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

        if (_tracker.IsRegistered(scanId))
            return ScanExecutionResult.Failure("Scan is already executing.");

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

        string xmlPath;
        try
        {
            _outputPaths.EnsureOutputDirectory();
            xmlPath = _outputPaths.CreateUniqueXmlPath(scan.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create output path for scan {ScanId}", scan.Id);
            return await FailScanAsync(scan, "Unable to prepare scan output directory.", cancellationToken);
        }

        if (!NmapArgumentBuilder.TryBuild(target.Address, profile, xmlPath, out var arguments, out var argError))
            return await FailScanAsync(scan, argError ?? "Unable to build safe scan arguments.", cancellationToken);

        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (!_tracker.TryRegister(scan.Id, linkedCts))
        {
            linkedCts.Dispose();
            return ScanExecutionResult.Failure("Scan is already executing.");
        }

        _logger.LogInformation(
            "Scan execution requested: ScanId={ScanId} TargetId={TargetId} Profile={Profile} Xml={Xml}",
            scan.Id, target.Id, profile.Name, xmlPath);

        scan.Status = ScanStatus.Running;
        scan.CurrentStage = ScanStage.Discovery;
        scan.StartedAt = DateTime.UtcNow;
        scan.CompletedAt = null;
        scan.StatusMessage = "Starting Nmap process…";
        scan.ProgressPercent = 1;
        scan.UpdatedAt = DateTime.UtcNow;
        scan.ErrorMessage = null;
        scan.OutputFilePath = xmlPath;

        try
        {
            await _scans.UpdateAsync(scan, cancellationToken);
        }
        catch (Exception ex)
        {
            _tracker.Unregister(scan.Id);
            _logger.LogError(ex, "Failed to mark scan {ScanId} as Running", scan.Id);
            return ScanExecutionResult.Failure("Unable to update scan status before execution.");
        }

        var request = new NmapRunRequest
        {
            ExecutablePath = availability.ExecutablePath!,
            Arguments = arguments,
            XmlOutputPath = xmlPath,
            WorkingDirectory = null
        };

        NmapRunResult processResult;
        try
        {
            processResult = await _nmap.RunAsync(request, linkedCts.Token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Nmap runner threw for scan {ScanId}", scan.Id);
            await FinalizeFailedAsync(scan, "Scan process failed unexpectedly.");
            return ScanExecutionResult.Failure("Scan process failed unexpectedly.");
        }
        finally
        {
            _tracker.Unregister(scan.Id);
        }

        if (!processResult.Started)
        {
            await FinalizeFailedAsync(scan, processResult.ErrorMessage ?? "Failed to start Nmap process.");
            TryDeletePartialOutput(xmlPath);
            return ScanExecutionResult.Failure(processResult.ErrorMessage ?? "Failed to start Nmap process.");
        }

        scan.CompletedAt = DateTime.UtcNow;
        scan.UpdatedAt = DateTime.UtcNow;
        scan.ProgressPercent = 100;
        scan.OutputFilePath = processResult.XmlOutputPath ?? xmlPath;

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
            scan.StatusMessage = "Nmap process completed successfully.";
            _logger.LogInformation(
                "Scan {ScanId} process completed. ExitCode={ExitCode} Duration={Duration} Xml={Xml}",
                scan.Id, processResult.ExitCode, processResult.Duration, scan.OutputFilePath);

            await TryPersistResultsAsync(scan, CancellationToken.None);
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

    private async Task TryPersistResultsAsync(Domain.Entities.Scan scan, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(scan.OutputFilePath))
            {
                scan.StatusMessage = "Nmap completed but no output file path was recorded; results were not persisted.";
                return;
            }

            var parseResult = await _xmlReader.ReadAsync(scan.OutputFilePath, cancellationToken).ConfigureAwait(false);
            if (!parseResult.Succeeded || parseResult.Scan is null)
            {
                var reason = parseResult.ErrorMessage ?? "XML parse failed.";
                _logger.LogWarning("Scan {ScanId} XML parse failed: {Reason}", scan.Id, reason);
                scan.StatusMessage = Truncate(
                    $"Nmap completed successfully, but result parsing failed: {reason}", 500);
                return;
            }

            var persistResult = await _persistence.PersistAsync(scan.Id, parseResult.Scan, cancellationToken)
                .ConfigureAwait(false);

            if (!persistResult.Succeeded)
            {
                var reason = persistResult.ErrorMessage ?? "Persistence failed.";
                _logger.LogWarning("Scan {ScanId} result persistence failed: {Reason}", scan.Id, reason);
                scan.StatusMessage = Truncate(
                    $"Nmap completed successfully, but result persistence failed: {reason}", 500);
                return;
            }

            scan.NmapScanner = parseResult.Scan.Scanner;
            scan.NmapVersion = parseResult.Scan.Version;
            scan.NmapArguments = parseResult.Scan.Arguments;
            scan.NmapElapsedSeconds = parseResult.Scan.ElapsedSeconds;
            scan.NmapSummary = parseResult.Scan.Summary;
            scan.NmapExitStatus = parseResult.Scan.ExitStatus;

            scan.StatusMessage = Truncate(
                $"Nmap completed. Persisted {persistResult.HostCount} host(s), {persistResult.PortCount} port(s).",
                500);
            _logger.LogInformation(
                "Scan {ScanId} results persisted: hosts={Hosts} ports={Ports}",
                scan.Id, persistResult.HostCount, persistResult.PortCount);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while persisting results for scan {ScanId}", scan.Id);
            scan.StatusMessage = Truncate(
                $"Nmap completed successfully, but result persistence error: {ex.Message}", 500);
        }
    }

    public bool RequestCancel(Guid scanId) => _tracker.TryCancel(scanId);

    private async Task FinalizeFailedAsync(Domain.Entities.Scan scan, string message)
    {
        scan.Status = ScanStatus.Failed;
        scan.CompletedAt = DateTime.UtcNow;
        scan.UpdatedAt = DateTime.UtcNow;
        scan.StatusMessage = message;
        scan.ErrorMessage = Truncate(message, 2000);
        scan.CurrentStage = ScanStage.Completed;
        scan.ProgressPercent = 0;
        try
        {
            await _scans.UpdateAsync(scan, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark scan {ScanId} as Failed after process error", scan.Id);
        }
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

    private void TryDeletePartialOutput(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not delete partial output {Path}", path);
        }
    }

    private static string? Truncate(string? value, int max) =>
        string.IsNullOrEmpty(value) ? value : value.Length <= max ? value : value[..max];
}
