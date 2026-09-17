using ANPT.Application.Interfaces;
using ANPT.Domain.Entities;
using ANPT.Domain.Enums;

namespace ANPT.Application.Analysis;

/// <summary>
/// Flags open ports where Nmap did not identify a service name.
/// </summary>
public sealed class ServiceWithoutIdentificationRule : IAnalysisRule
{
    public string RuleId => "SERVICE-WITHOUT-IDENTIFICATION";
    public string Name => "Service Without Identification";
    public string Description => "Open ports where service identification metadata is missing.";

    public IReadOnlyList<Finding> Analyze(Scan scan, IReadOnlyList<Host> hosts)
    {
        var findings = new List<Finding>();

        foreach (var host in hosts)
        {
            foreach (var svc in host.Services)
            {
                if (!string.Equals(svc.State, "open", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!string.IsNullOrWhiteSpace(svc.ServiceName))
                    continue;

                var hostLabel = HostLabel(host);

                findings.Add(new Finding
                {
                    ScanId = scan.Id,
                    HostId = host.Id,
                    ServiceId = svc.Id,
                    RuleId = RuleId,
                    Title = $"Open {svc.Protocol.ToUpperInvariant()} port {svc.Port} without service identification on {hostLabel}",
                    Description =
                        $"Port {svc.Port}/{svc.Protocol} is open on {hostLabel}, but Nmap did not report a service name. " +
                        $"Service identification may be incomplete (version detection not used, filtered probes, or unknown service). " +
                        $"This is not a confirmed vulnerability.",
                    Severity = Severity.Low,
                    Confidence = 0.7,
                    Status = FindingStatus.Detected,
                    Evidence =
                        $"Host={hostLabel}; Protocol={svc.Protocol}; Port={svc.Port}; State={svc.State}; " +
                        $"ServiceName=(none); DetectionMethod={svc.DetectionMethod ?? "(none)"}; " +
                        $"Reason={svc.StateReason ?? "(none)"}",
                    Impact = "Unknown services make risk assessment harder and may hide unexpected listeners.",
                    Recommendation =
                        "Re-scan with service version detection where authorized, or manually identify the listening process " +
                        "and document the expected service inventory.",
                    AffectedPort = $"{svc.Protocol}/{svc.Port}",
                    AffectedService = null
                });
            }
        }

        return findings;
    }

    private static string HostLabel(Host host)
    {
        if (!string.IsNullOrWhiteSpace(host.IpAddress))
            return host.IpAddress;
        if (!string.IsNullOrWhiteSpace(host.Hostname))
            return host.Hostname!;
        if (!string.IsNullOrWhiteSpace(host.MacAddress))
            return host.MacAddress!;
        return host.Id.ToString("N")[..8];
    }
}
