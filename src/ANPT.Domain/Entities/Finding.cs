using ANPT.Domain.Enums;

namespace ANPT.Domain.Entities;

public class Finding : BaseEntity
{
    public Guid ScanId { get; set; }
    public Scan? Scan { get; set; }

    public Guid? HostId { get; set; }
    public Host? Host { get; set; }

    public Guid? ServiceId { get; set; }
    public Service? Service { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Severity Severity { get; set; } = Severity.Info;
    public double Confidence { get; set; }
    public FindingStatus Status { get; set; } = FindingStatus.Detected;
    public string? Evidence { get; set; }
    public string? Impact { get; set; }
    public string? Recommendation { get; set; }
    public string? Reference { get; set; }
    public string? AffectedPort { get; set; }
    public string? AffectedService { get; set; }
}
