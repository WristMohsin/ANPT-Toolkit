namespace ANPT.Application.Models.Nmap;

/// <summary>
/// Result of an Nmap XML parse or controlled file read attempt.
/// </summary>
public sealed class NmapParseResult
{
    public bool Succeeded { get; private init; }
    public string? ErrorMessage { get; private init; }
    public NmapScanResult? Scan { get; private init; }

    public static NmapParseResult Success(NmapScanResult scan) =>
        new() { Succeeded = true, Scan = scan };

    public static NmapParseResult Failure(string message) =>
        new() { Succeeded = false, ErrorMessage = message };
}
