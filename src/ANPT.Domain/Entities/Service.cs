namespace ANPT.Domain.Entities;

public class Service : BaseEntity
{
    public Guid HostId { get; set; }
    public Host? Host { get; set; }

    public int Port { get; set; }
    public string Protocol { get; set; } = "tcp";
    public string State { get; set; } = "open";
    public string? ServiceName { get; set; }
    public string? Product { get; set; }
    public string? Version { get; set; }
    public string? Banner { get; set; }
    public double? Confidence { get; set; }
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
}
