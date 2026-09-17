using ANPT.Domain.Enums;

namespace ANPT.Application.Interfaces;

public interface ISecurityAnalysisService
{
    /// <summary>
    /// Runs all registered analysis rules against a completed scan's persisted hosts/services
    /// and persists findings idempotently (replaces prior analysis findings for the scan).
    /// </summary>
    Task<SecurityAnalysisResult> AnalyzeScanAsync(Guid scanId, CancellationToken cancellationToken = default);
}

public sealed class SecurityAnalysisResult
{
    public bool Succeeded { get; private init; }
    public string? ErrorMessage { get; private init; }
    public Guid ScanId { get; private init; }
    public int RulesExecuted { get; private init; }
    public int FindingsCreated { get; private init; }
    public int FindingsRemoved { get; private init; }
    public TimeSpan Duration { get; private init; }

    public static SecurityAnalysisResult Success(
        Guid scanId,
        int rulesExecuted,
        int findingsCreated,
        int findingsRemoved,
        TimeSpan duration) =>
        new()
        {
            Succeeded = true,
            ScanId = scanId,
            RulesExecuted = rulesExecuted,
            FindingsCreated = findingsCreated,
            FindingsRemoved = findingsRemoved,
            Duration = duration
        };

    public static SecurityAnalysisResult Failure(string message) =>
        new() { Succeeded = false, ErrorMessage = message };
}
