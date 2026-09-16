using ANPT.Domain.Enums;

namespace ANPT.Domain.Entities;

public class Target : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string TargetType { get; set; } = "Host";
    public string Address { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool AuthorizationConfirmed { get; set; }
    public TargetStatus Status { get; set; } = TargetStatus.Active;
    public string? CreatedBy { get; set; }

    public ICollection<Scan> Scans { get; set; } = new List<Scan>();
}
