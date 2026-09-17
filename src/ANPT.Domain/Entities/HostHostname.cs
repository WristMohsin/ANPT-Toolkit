namespace ANPT.Domain.Entities;

/// <summary>
/// One hostname observed on a host (PTR, user, etc.).
/// </summary>
public class HostHostname : BaseEntity
{
    public Guid HostId { get; set; }
    public Host? Host { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Nmap hostname type (e.g. PTR, user).
    /// </summary>
    public string? Type { get; set; }
}
