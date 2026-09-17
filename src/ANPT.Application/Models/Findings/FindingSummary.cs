using ANPT.Domain.Enums;

namespace ANPT.Application.Models.Findings;

/// <summary>
/// Aggregate counts derived from persisted findings only (no hard-coded values).
/// </summary>
public sealed class FindingSummary
{
    public int Total { get; init; }
    public int Informational { get; init; }
    public int Low { get; init; }
    public int Medium { get; init; }
    public int High { get; init; }
    public int Critical { get; init; }
    public int Open { get; init; }
    public int Remediated { get; init; }
    public int FalsePositive { get; init; }
    public IReadOnlyDictionary<string, int> ByRule { get; init; } =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
}
