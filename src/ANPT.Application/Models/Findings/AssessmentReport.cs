namespace ANPT.Application.Models.Findings;

/// <summary>
/// Report-ready foundation built entirely from persisted scan and finding data.
/// Not a PDF generator \u2014 a structured read model for future professional reporting.
/// </summary>
public sealed class AssessmentReport
{
    public Guid ScanId { get; init; }
    public string ScanName { get; init; } = string.Empty;
    public string? TargetName { get; init; }
    public string? TargetAddress { get; init; }
    public DateTime? ScanStartedAt { get; init; }
    public DateTime? ScanCompletedAt { get; init; }
    public string? ScanStatus { get; init; }
    public string? ProfileName { get; init; }

    public FindingSummary Summary { get; init; } = new();
    public int HostCount { get; init; }
    public int ServiceCount { get; init; }

    public IReadOnlyList<FindingDetailDto> Findings { get; init; } = Array.Empty<FindingDetailDto>();

    public IReadOnlyList<string> TopRecommendations { get; init; } = Array.Empty<string>();
}
