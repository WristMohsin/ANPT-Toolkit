using ANPT.Application.Services;

namespace ANPT.Tests.Application;

public class ApplicationInfoTests
{
    [Fact]
    public void ApplicationInfo_HasExpectedValues()
    {
        var info = new ApplicationInfo();
        Assert.Equal("Automated Network Penetration Testing Toolkit", info.ApplicationName);
        Assert.Equal("ANPT Toolkit", info.ShortName);
        Assert.False(string.IsNullOrWhiteSpace(info.Version));
        Assert.Contains("authorized", info.Description, StringComparison.OrdinalIgnoreCase);
    }
}
