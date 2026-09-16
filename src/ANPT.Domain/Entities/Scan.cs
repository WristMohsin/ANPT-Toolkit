using ANPT.Domain.Enums;

namespace ANPT.Domain.Entities;

public class Scan : BaseEntity
{
    public Guid TargetId { get; set; }
    public Target? Target { get; set; }

    public Guid ScanProfileId { get; set; }
    public ScanProfile? ScanProfile { get; set; }

    public string Name { get; set; } = string.Empty;
    public ScanStatus Status { get; set; } = ScanStatus.Queued;
    public ScanStage CurrentStage { get; set; } = ScanStage.Initializing;
    public int ProgressPercent { get; set; }
    public string? StatusMessage { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public string? CreatedBy { get; set; }

    /// <summary>
    /// Absolute path to the Nmap XML output artifact under the controlled ScanOutput directory.
    /// Set when execution starts; retained for later parsing (Phase 5C). Not parsed in Phase 5B.
    /// </summary>
    public string? OutputFilePath { get; set; }

    public ICollection<Host> Hosts { get; set; } = new List<Host>();
    public ICollection<Finding> Findings { get; set; } = new List<Finding>();
}
