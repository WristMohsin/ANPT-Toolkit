namespace ANPT.Domain.Entities;

public class AuditLog : BaseEntity
{
    public string? Username { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? ResourceType { get; set; }
    public string? ResourceId { get; set; }
    public string Result { get; set; } = "Success";
    public string? Details { get; set; }
    public string? IpAddress { get; set; }
}
