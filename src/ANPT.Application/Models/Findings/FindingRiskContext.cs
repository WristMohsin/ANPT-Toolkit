using ANPT.Domain.Enums;

namespace ANPT.Application.Models.Findings;

/// <summary>
/// Deterministic risk/priority context derived from finding severity and rule category.
/// Does not invent CVSS scores or CVE identifiers.
/// </summary>
public sealed class FindingRiskContext
{
    public Severity Severity { get; init; }
    public FindingPriority Priority { get; init; }
    public string RiskLabel { get; init; } = string.Empty;
    public string Rationale { get; init; } = string.Empty;

    /// <summary>
    /// Maps severity to priority using a fixed, documented table.
    /// </summary>
    public static FindingRiskContext FromSeverity(Severity severity, string? ruleId = null)
    {
        var (priority, label, rationale) = severity switch
        {
            Severity.Critical => (
                FindingPriority.Urgent,
                "Urgent review",
                "Critical severity indicates the highest recommended review urgency among exposure observations."),
            Severity.High => (
                FindingPriority.High,
                "High priority review",
                "High severity exposure should be reviewed promptly within the assessment workflow."),
            Severity.Medium => (
                FindingPriority.Elevated,
                "Elevated review",
                "Medium severity indicates elevated exposure that warrants planned review."),
            Severity.Low => (
                FindingPriority.Routine,
                "Routine review",
                "Low severity exposure is recorded for completeness and routine review."),
            _ => (
                FindingPriority.Routine,
                "Informational exposure",
                "Informational findings document observed exposure and are not confirmed vulnerabilities.")
        };

        if (!string.IsNullOrWhiteSpace(ruleId) &&
            ruleId.Equals("SENSITIVE-SERVICE-EXPOSURE", StringComparison.OrdinalIgnoreCase) &&
            severity < Severity.High)
        {
            if (priority < FindingPriority.Elevated)
            {
                priority = FindingPriority.Elevated;
                label = "Elevated review (sensitive service)";
                rationale =
                    "Sensitive service exposure is prioritized for review even when severity is not Critical.";
            }
        }

        return new FindingRiskContext
        {
            Severity = severity,
            Priority = priority,
            RiskLabel = label,
            Rationale = rationale
        };
    }
}
