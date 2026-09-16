using ANPT.Application.Models.Nmap;

namespace ANPT.Application.Interfaces;

/// <summary>
/// Parses Nmap XML into typed result models. Does not execute Nmap or access the database.
/// </summary>
public interface INmapXmlParser
{
    /// <summary>
    /// Parses Nmap XML text. Returns Failure for empty, malformed, or non-nmaprun documents.
    /// Does not resolve external entities or DTDs.
    /// </summary>
    NmapParseResult Parse(string xml);
}
