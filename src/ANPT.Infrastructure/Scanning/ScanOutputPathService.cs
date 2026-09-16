using ANPT.Application.Interfaces;
using ANPT.Infrastructure.Configuration;

namespace ANPT.Infrastructure.Scanning;

public sealed class ScanOutputPathService : IScanOutputPathService
{
    public void EnsureOutputDirectory()
    {
        Directory.CreateDirectory(AppPaths.ScanOutputDirectory);
    }

    public string CreateUniqueXmlPath(Guid scanId)
    {
        EnsureOutputDirectory();
        var stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
        var fileName = $"scan-{scanId:N}-{stamp}.xml";
        return Path.Combine(AppPaths.ScanOutputDirectory, fileName);
    }
}
