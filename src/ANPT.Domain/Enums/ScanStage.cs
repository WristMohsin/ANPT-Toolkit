namespace ANPT.Domain.Enums;

public enum ScanStage
{
    Initializing = 0,
    Discovery = 1,
    PortScanning = 2,
    ServiceEnumeration = 3,
    VulnerabilityAssessment = 4,
    Correlation = 5,
    Finalizing = 6,
    Completed = 7
}
