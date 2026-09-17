namespace ANPT.Domain.Entities;

/// <summary>
/// One network address observed on a host (IPv4, IPv6, or MAC).
/// </summary>
public class HostAddress : BaseEntity
{
    public Guid HostId { get; set; }
    public Host? Host { get; set; }

    public string Address { get; set; } = string.Empty;

    /// <summary>
    /// Nmap addrtype: ipv4, ipv6, mac.
    /// </summary>
    public string AddressType { get; set; } = string.Empty;

    public string? Vendor { get; set; }
}
