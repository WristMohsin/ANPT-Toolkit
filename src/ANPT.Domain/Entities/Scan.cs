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
    /// </summary>
    public string? OutputFilePath { get; set; }

    /// <summary>Nmap scanner name from parsed XML (e.g. nmap).</summary>
    public string? NmapScanner { get; set; }

    /// <summary>Nmap version string from parsed XML.</summary>
    public string? NmapVersion { get; set; }

    /// <summary>Nmap command arguments recorded in the XML.</summary>
    public string? NmapArguments { get; set; }

    /// <summary>Elapsed seconds reported by Nmap runstats.</summary>
    public double? NmapElapsedSeconds { get; set; }

    /// <summary>Nmap finished summary text.</summary>
    public string? NmapSummary { get; set; }

    /// <summary>Nmap exit status from runstats (e.g. success).</summary>
    public string? NmapExitStatus { get; set; }

    public ICollection<Host> Hosts { get; set; } = new List<Host>();
    public ICollection<Finding> Findings { get; set; } = new List<Finding>();
}
