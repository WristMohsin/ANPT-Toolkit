namespace ANPT.Domain.Entities;

/// <summary>
/// A discovered port (and optional service details) on a host.
/// A port may exist without service identification fields.
/// </summary>
public class Service : BaseEntity
{
    public Guid HostId { get; set; }
    public Host? Host { get; set; }

    public int Port { get; set; }
    public string Protocol { get; set; } = "tcp";
    public string State { get; set; } = "open";

    /// <summary>
    /// Nmap port state reason (e.g. syn-ack, reset, no-response).
    /// </summary>
    public string? StateReason { get; set; }

    public string? ServiceName { get; set; }
    public string? Product { get; set; }
    public string? Version { get; set; }

    /// <summary>
    /// Extra service info / banner-like text from Nmap extrainfo.
    /// </summary>
    public string? Banner { get; set; }

    public string? Tunnel { get; set; }
    public string? DetectionMethod { get; set; }
    public double? Confidence { get; set; }
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
}
