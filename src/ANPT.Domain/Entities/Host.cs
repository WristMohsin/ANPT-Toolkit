namespace ANPT.Domain.Entities;

/// <summary>
/// A host discovered during a scan. Primary identity fields (IpAddress / MacAddress / Hostname)
/// hold the preferred summary values; full multi-value data lives in Addresses and Hostnames.
/// </summary>
public class Host : BaseEntity
{
    public Guid ScanId { get; set; }
    public Scan? Scan { get; set; }

    /// <summary>
    /// Preferred IPv4 (or first usable address). Empty when the host has no IPv4.
    /// </summary>
    public string IpAddress { get; set; } = string.Empty;

    /// <summary>
    /// Preferred hostname (PTR or first hostname), if any.
    /// </summary>
    public string? Hostname { get; set; }

    /// <summary>
    /// Preferred MAC address, if any.
    /// </summary>
    public string? MacAddress { get; set; }

    public string? OsInfo { get; set; }

    /// <summary>
    /// Host state from Nmap (e.g. up, down, unknown).
    /// </summary>
    public string Status { get; set; } = "up";

    /// <summary>
    /// Nmap status reason (e.g. syn-ack, echo-reply).
    /// </summary>
    public string? StateReason { get; set; }

    public DateTime DiscoveredAt { get; set; } = DateTime.UtcNow;

    public ICollection<HostAddress> Addresses { get; set; } = new List<HostAddress>();
    public ICollection<HostHostname> Hostnames { get; set; } = new List<HostHostname>();
    public ICollection<Service> Services { get; set; } = new List<Service>();
}
