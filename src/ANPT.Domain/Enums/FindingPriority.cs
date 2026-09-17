namespace ANPT.Domain.Enums;

/// <summary>
/// Recommended review urgency derived deterministically from Severity.
/// Distinct from Severity (security impact classification).
/// </summary>
public enum FindingPriority
{
    /// <summary>Routine informational review.</summary>
    Routine = 0,
    /// <summary>Should be reviewed in normal workflow.</summary>
    Elevated = 1,
    /// <summary>Prioritize for near-term review.</summary>
    High = 2,
    /// <summary>Urgent security review recommended.</summary>
    Urgent = 3
}
