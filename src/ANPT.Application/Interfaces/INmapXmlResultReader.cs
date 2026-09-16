using ANPT.Application.Models.Nmap;

namespace ANPT.Application.Interfaces;

/// <summary>
/// Reads a controlled ScanOutput XML file and parses it via <see cref="INmapXmlParser"/>.
/// Rejects paths outside the application ScanOutput directory.
/// </summary>
public interface INmapXmlResultReader
{
    /// <summary>
    /// Reads and parses the XML at the given absolute path if it is under ScanOutput.
    /// </summary>
    Task<NmapParseResult> ReadAsync(string xmlFilePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads and parses XML using <see cref="Domain.Entities.Scan.OutputFilePath"/>.
    /// </summary>
    Task<NmapParseResult> ReadForScanAsync(Domain.Entities.Scan scan, CancellationToken cancellationToken = default);
}
