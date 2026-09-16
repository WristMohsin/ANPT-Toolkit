namespace ANPT.Application.Models.Nmap;

/// <summary>
/// Typed representation of a parsed Nmap XML document.
/// Not an EF entity — used only for in-memory parse results (Phase 5C).
/// </summary>
public sealed class NmapScanResult
{
    public string? Scanner { get; init; }
    public string? Version { get; init; }
    public string? Arguments { get; init; }
    public string? StartTimeRaw { get; init; }
    public DateTimeOffset? StartTime { get; init; }
    public double? ElapsedSeconds { get; init; }
    public string? Summary { get; init; }
    public string? ExitStatus { get; init; }
    public IReadOnlyList<NmapHostResult> Hosts { get; init; } = Array.Empty<NmapHostResult>();
}

public sealed class NmapHostResult
{
    public string? State { get; init; }
    public string? StateReason { get; init; }
    public IReadOnlyList<NmapAddressResult> Addresses { get; init; } = Array.Empty<NmapAddressResult>();
    public IReadOnlyList<NmapHostnameResult> Hostnames { get; init; } = Array.Empty<NmapHostnameResult>();
    public IReadOnlyList<NmapPortResult> Ports { get; init; } = Array.Empty<NmapPortResult>();
}

public sealed class NmapAddressResult
{
    public string? Address { get; init; }
    public string? AddressType { get; init; }
    public string? Vendor { get; init; }
}

public sealed class NmapHostnameResult
{
    public string? Name { get; init; }
    public string? Type { get; init; }
}

public sealed class NmapPortResult
{
    public string? Protocol { get; init; }
    public int? PortId { get; init; }
    public string? State { get; init; }
    public string? Reason { get; init; }
    public NmapServiceResult? Service { get; init; }
}

public sealed class NmapServiceResult
{
    public string? Name { get; init; }
    public string? Product { get; init; }
    public string? Version { get; init; }
    public string? ExtraInfo { get; init; }
    public string? Tunnel { get; init; }
    public string? Method { get; init; }
    public int? Confidence { get; init; }
}
