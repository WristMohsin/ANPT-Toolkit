namespace ANPT.Domain.Entities;

public class ScanProfile : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IncludeHostDiscovery { get; set; } = true;
    public bool IncludePortScanning { get; set; }
    public bool IncludeServiceEnumeration { get; set; }
    public bool IncludeVulnerabilityAssessment { get; set; }
    public bool IncludeCorrelation { get; set; }
    public bool IsBuiltIn { get; set; }
    public bool IsEnabled { get; set; } = true;
}
