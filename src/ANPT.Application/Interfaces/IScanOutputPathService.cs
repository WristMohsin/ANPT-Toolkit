namespace ANPT.Application.Interfaces;

/// <summary>
/// Produces controlled, unique Nmap XML output paths under the application ScanOutput directory.
/// </summary>
public interface IScanOutputPathService
{
    /// <summary>
    /// Returns an absolute path under the controlled ScanOutput root for the given scan.
    /// Filename is unique per call.
    /// </summary>
    string CreateUniqueXmlPath(Guid scanId);

    /// <summary>
    /// Ensures the ScanOutput directory exists.
    /// </summary>
    void EnsureOutputDirectory();
}
