namespace ANPT.Domain.Enums;

public enum FindingStatus
{
    Detected = 0,
    Potential = 1,
    Confirmed = 2,
    NeedsVerification = 3,
    FalsePositive = 4,
    Remediated = 5
}
