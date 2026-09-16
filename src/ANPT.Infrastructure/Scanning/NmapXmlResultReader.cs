using ANPT.Application.Interfaces;
using ANPT.Application.Models.Nmap;
using ANPT.Domain.Entities;
using ANPT.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;

namespace ANPT.Infrastructure.Scanning;

/// <summary>
/// Reads Nmap XML only from the controlled ScanOutput directory and delegates parsing.
/// </summary>
public sealed class NmapXmlResultReader : INmapXmlResultReader
{
    private readonly INmapXmlParser _parser;
    private readonly ILogger<NmapXmlResultReader> _logger;

    public NmapXmlResultReader(INmapXmlParser parser, ILogger<NmapXmlResultReader> logger)
    {
        _parser = parser;
        _logger = logger;
    }

    public async Task<NmapParseResult> ReadAsync(string xmlFilePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(xmlFilePath))
            return NmapParseResult.Failure("XML file path is required.");

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(xmlFilePath.Trim());
        }
        catch (Exception ex)
        {
            return NmapParseResult.Failure($"Invalid file path: {ex.Message}");
        }

        if (!IsUnderScanOutput(fullPath))
        {
            _logger.LogWarning("Rejected XML path outside ScanOutput: {Path}", fullPath);
            return NmapParseResult.Failure("XML path is outside the allowed ScanOutput directory.");
        }

        if (!File.Exists(fullPath))
            return NmapParseResult.Failure("Scan output XML file was not found.");

        string content;
        try
        {
            content = await File.ReadAllTextAsync(fullPath, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read XML file {Path}", fullPath);
            return NmapParseResult.Failure($"Unable to read scan output file: {ex.Message}");
        }

        if (string.IsNullOrWhiteSpace(content))
            return NmapParseResult.Failure("Scan output XML file is empty.");

        return _parser.Parse(content);
    }

    public Task<NmapParseResult> ReadForScanAsync(Scan scan, CancellationToken cancellationToken = default)
    {
        if (scan is null)
            return Task.FromResult(NmapParseResult.Failure("Scan is required."));

        if (string.IsNullOrWhiteSpace(scan.OutputFilePath))
            return Task.FromResult(NmapParseResult.Failure("Scan has no output file path. Run the scan first."));

        return ReadAsync(scan.OutputFilePath, cancellationToken);
    }

    public static bool IsUnderScanOutput(string fullPath)
    {
        try
        {
            var root = Path.GetFullPath(AppPaths.ScanOutputDirectory)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var candidate = Path.GetFullPath(fullPath);
            var candidateDir = Path.GetDirectoryName(candidate);
            if (candidateDir is null)
                return false;

            candidateDir = Path.GetFullPath(candidateDir)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            return candidateDir.Equals(root, StringComparison.OrdinalIgnoreCase)
                   || candidateDir.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
