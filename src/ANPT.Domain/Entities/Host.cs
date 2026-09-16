namespace ANPT.Domain.Entities;

public class Host : BaseEntity
{
    public Guid ScanId { get; set; }
    public Scan? Scan { get; set; }

    public string IpAddress { get; set; } = string.Empty;
    public string? Hostname { get; set; }
    public string? MacAddress { get; set; }
    public string? OsInfo { get; set; }
    public string Status { get; set; } = "Up";
    public DateTime DiscoveredAt { get; set; } = DateTime.UtcNow;

    public ICollection<Service> Services { get; set; } = new List<Service>();
}
