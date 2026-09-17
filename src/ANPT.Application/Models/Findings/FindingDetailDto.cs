using ANPT.Domain.Enums;

namespace ANPT.Application.Models.Findings;

/// <summary>
/// Enriched read model for professional finding presentation and report foundation.
/// Built from persisted Finding + Host + Service + Scan relationships.
/// </summary>
public sealed class FindingDetailDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public Severity Severity { get; init; }
    public FindingStatus Status { get; init; }
    public FindingPriority Priority { get; init; }
    public string RiskLabel { get; init; } = string.Empty;
    public string RiskRationale { get; init; } = string.Empty;
    public string? RuleId { get; init; }
    public string? Category { get; init; }
    public string? Evidence { get; init; }
    public string? Impact { get; init; }
    public string? Recommendation { get; init; }
    public string? Reference { get; init; }
    public double Confidence { get; init; }
    public DateTime CreatedAt { get; init; }

    public Guid? HostId { get; init; }
    public string HostDisplay { get; init; } = "\u2014";
    public string? HostIp { get; init; }
    public string? HostHostname { get; init; }
    public string? HostMac { get; init; }

    public Guid? ServiceId { get; init; }
    public string? Protocol { get; init; }
    public int? Port { get; init; }
    public string? ServiceName { get; init; }
    public string? Product { get; init; }
    public string? Version { get; init; }
    public string? PortState { get; init; }
    public string? AffectedPort { get; init; }
    public string? AffectedService { get; init; }

    public Guid ScanId { get; init; }
    public string ScanName { get; init; } = "\u2014";
    public string? TargetName { get; init; }
    public string? TargetAddress { get; init; }
    public DateTime? ScanCompletedAt { get; init; }
}
