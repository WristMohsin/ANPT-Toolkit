using ANPT.Application.Interfaces;
using ANPT.Domain.Entities;
using ANPT.Domain.Enums;

namespace ANPT.Application.Analysis;

/// <summary>
/// Reports open TCP/UDP ports as informational exposure observations.
/// Does not claim a vulnerability — only that the service/port is reachable.
/// </summary>
public sealed class OpenServiceExposureRule : IAnalysisRule
{
    public string RuleId => "OPEN-SERVICE-EXPOSURE";
    public string Name => "Open Service Exposure";
    public string Description => "Identifies hosts with open ports/services observed by Nmap.";

    public string Category => "Network Exposure";

    public IReadOnlyList<Finding> Analyze(Scan scan, IReadOnlyList<Host> hosts)
    {
        var findings = new List<Finding>();

        foreach (var host in hosts)
        {
            foreach (var svc in host.Services)
            {
                if (!IsOpen(svc.State))
                    continue;

                var hostLabel = HostLabel(host);
                var serviceLabel = string.IsNullOrWhiteSpace(svc.ServiceName)
                    ? "unidentified service"
                    : svc.ServiceName;

                findings.Add(new Finding
                {
                    ScanId = scan.Id,
                    HostId = host.Id,
                    ServiceId = svc.Id,
                    RuleId = RuleId,
                    Title = $"Open {svc.Protocol.ToUpperInvariant()} port {svc.Port} ({serviceLabel}) on {hostLabel}",
                    Description =
                        $"Nmap observed an open {svc.Protocol.ToUpperInvariant()} port {svc.Port} on host {hostLabel}. " +
                        $"This is an exposure observation, not a confirmed vulnerability. " +
                        $"Review whether this service should be reachable from the assessment network.",
                    Severity = Severity.Info,
                    Confidence = 0.9,
                    Status = FindingStatus.Detected,
                    Evidence =
                        $"Host={hostLabel}; Protocol={svc.Protocol}; Port={svc.Port}; State={svc.State}; " +
                        $"ServiceName={svc.ServiceName ?? "(none)"}; Product={svc.Product ?? "(none)"}; " +
                        $"Version={svc.Version ?? "(none)"}; Reason={svc.StateReason ?? "(none)"}",
                    Impact = "Exposed services increase the attack surface available to network adversaries.",
                    Recommendation =
                        "Confirm the service is required, restrict access with host/network controls where appropriate, " +
                        "and ensure the service is patched and configured securely.",
                    AffectedPort = $"{svc.Protocol}/{svc.Port}",
                    AffectedService = svc.ServiceName
                });
            }
        }

        return findings;
    }

    private static bool IsOpen(string? state) =>
        string.Equals(state, "open", StringComparison.OrdinalIgnoreCase);

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
