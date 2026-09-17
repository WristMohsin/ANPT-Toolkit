using ANPT.Application.Interfaces;
using ANPT.Domain.Entities;
using ANPT.Domain.Enums;

namespace ANPT.Application.Analysis;

/// <summary>
/// Flags commonly sensitive services when observed as open.
/// Does not claim confirmed vulnerability or exploitability.
/// </summary>
public sealed class SensitiveServiceExposureRule : IAnalysisRule
{
    public string RuleId => "SENSITIVE-SERVICE-EXPOSURE";
    public string Name => "Sensitive Service Exposure";
    public string Description => "Open ports associated with commonly sensitive network services.";

    public string Category => "Sensitive Services";

    private static readonly Dictionary<int, (string Label, string Note)> SensitivePorts = new()
    {
        [21] = ("FTP", "FTP commonly transfers credentials and data without modern encryption unless FTPS is enforced."),
        [23] = ("Telnet", "Telnet commonly transmits credentials and session data without encryption."),
        [445] = ("SMB", "SMB exposure should be reviewed; historically a high-value target for lateral movement."),
        [139] = ("NetBIOS/SMB", "Legacy NetBIOS/SMB-related exposure should be reviewed on modern networks."),
        [3389] = ("RDP", "Remote Desktop exposure should be tightly controlled and monitored."),
        [5900] = ("VNC", "VNC remote access should use strong authentication and encryption."),
        [1433] = ("MSSQL", "Database listeners should not be exposed beyond required administrative networks."),
        [3306] = ("MySQL", "Database listeners should not be exposed beyond required administrative networks."),
        [5432] = ("PostgreSQL", "Database listeners should not be exposed beyond required administrative networks."),
        [1521] = ("Oracle", "Database listeners should not be exposed beyond required administrative networks."),
        [6379] = ("Redis", "Redis is often deployed without authentication by default and warrants review when exposed."),
        [27017] = ("MongoDB", "MongoDB exposure should be reviewed for authentication and network restriction."),
        [11211] = ("Memcached", "Memcached exposure has historically been abused when left open without controls.")
    };

    private static readonly HashSet<string> SensitiveServiceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "ftp", "telnet", "microsoft-ds", "netbios-ssn", "smb", "ms-wbt-server", "rdp",
        "vnc", "ms-sql-s", "mysql", "postgresql", "oracle", "redis", "mongodb", "memcache", "memcached"
    };

    public IReadOnlyList<Finding> Analyze(Scan scan, IReadOnlyList<Host> hosts)
    {
        var findings = new List<Finding>();

        foreach (var host in hosts)
        {
            foreach (var svc in host.Services)
            {
                if (!string.Equals(svc.State, "open", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!IsSensitive(svc, out var label, out var note))
                    continue;

                var hostLabel = HostLabel(host);

                findings.Add(new Finding
                {
                    ScanId = scan.Id,
                    HostId = host.Id,
                    ServiceId = svc.Id,
                    RuleId = RuleId,
                    Title = $"Potentially sensitive service exposed: {label} on {hostLabel} ({svc.Protocol}/{svc.Port})",
                    Description =
                        $"Nmap observed an open {svc.Protocol}/{svc.Port} service on {hostLabel} identified as {label}. " +
                        $"{note} " +
                        $"This finding indicates exposure that warrants review; it does not by itself prove a vulnerability or successful attack path.",
                    Severity = Severity.Medium,
                    Confidence = 0.8,
                    Status = FindingStatus.Detected,
                    Evidence =
                        $"Host={hostLabel}; Protocol={svc.Protocol}; Port={svc.Port}; State={svc.State}; " +
                        $"ServiceName={svc.ServiceName ?? "(none)"}; Product={svc.Product ?? "(none)"}; " +
                        $"Version={svc.Version ?? "(none)"}",
                    Impact = "Sensitive services expand the set of high-value targets available on the network.",
                    Recommendation =
                        "Verify business need for network exposure, restrict source access, enforce encryption and strong authentication, " +
                        "and keep the service patched. Prefer modern secure alternatives where applicable (e.g. SSH instead of Telnet).",
                    AffectedPort = $"{svc.Protocol}/{svc.Port}",
                    AffectedService = svc.ServiceName ?? label
                });
            }
        }

        return findings;
    }

    private static bool IsSensitive(Service svc, out string label, out string note)
    {
        if (SensitivePorts.TryGetValue(svc.Port, out var byPort))
        {
            label = byPort.Label;
            note = byPort.Note;
            return true;
        }

        if (!string.IsNullOrWhiteSpace(svc.ServiceName) && SensitiveServiceNames.Contains(svc.ServiceName.Trim()))
        {
            label = svc.ServiceName;
            note = "This service is commonly treated as sensitive when reachable on a network.";
            return true;
        }

        label = string.Empty;
        note = string.Empty;
        return false;
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
