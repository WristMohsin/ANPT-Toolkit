using ANPT.Domain.Entities;

namespace ANPT.Application.Interfaces;

/// <summary>
/// Deterministic, non-intrusive analysis rule that operates on persisted scan data only.
/// </summary>
public interface IAnalysisRule
{
    /// <summary>Stable rule identifier used for deduplication (e.g. OPEN-SERVICE-EXPOSURE).</summary>
    string RuleId { get; }

    /// <summary>Human-readable rule name.</summary>
    string Name { get; }

    /// <summary>Short description of what the rule evaluates.</summary>
    string Description { get; }

    /// <summary>Rule category for filtering and presentation (e.g. Network Exposure).</summary>
    string Category { get; }

    /// <summary>
    /// Analyze the given scan graph and return zero or more candidate findings.
    /// Must not perform network I/O.
    /// </summary>
    IReadOnlyList<Finding> Analyze(Scan scan, IReadOnlyList<Host> hosts);
}
