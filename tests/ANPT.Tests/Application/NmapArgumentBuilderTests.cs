using ANPT.Application.Services;
using ANPT.Domain.Entities;

namespace ANPT.Tests.Application;

public class NmapArgumentBuilderTests
{
    [Fact]
    public void DiscoveryOnly_UsesSn()
    {
        var profile = new ScanProfile
        {
            IncludeHostDiscovery = true,
            IncludePortScanning = false,
            IncludeServiceEnumeration = false
        };
        Assert.True(NmapArgumentBuilder.TryBuild("10.0.0.1", profile, null, out var args, out _));
        Assert.Contains("-sn", args);
        Assert.DoesNotContain("-sT", args);
        Assert.Equal("10.0.0.1", args[^1]);
    }

    [Fact]
    public void PortScan_UsesStAndTopPorts()
    {
        var profile = new ScanProfile
        {
            IncludeHostDiscovery = true,
            IncludePortScanning = true,
            IncludeServiceEnumeration = false
        };
        Assert.True(NmapArgumentBuilder.TryBuild("192.168.1.10", profile, null, out var args, out _));
        Assert.Contains("-sT", args);
        Assert.Contains("--top-ports", args);
        Assert.DoesNotContain("-sV", args);
    }

    [Fact]
    public void ServiceEnum_AddsSv()
    {
        var profile = new ScanProfile
        {
            IncludePortScanning = true,
            IncludeServiceEnumeration = true
        };
        Assert.True(NmapArgumentBuilder.TryBuild("host.example.com", profile, null, out var args, out _));
        Assert.Contains("-sV", args);
        Assert.Contains("--version-light", args);
    }

    [Fact]
    public void XmlPath_AddsOx()
    {
        var profile = new ScanProfile { IncludeHostDiscovery = true };
        Assert.True(NmapArgumentBuilder.TryBuild("10.0.0.1", profile, @"C:\app\out.xml", out var args, out _));
        Assert.Contains("-oX", args);
        Assert.Contains(@"C:\app\out.xml", args);
    }

    [Fact]
    public void EmptyTarget_Fails()
    {
        var profile = new ScanProfile { IncludeHostDiscovery = true };
        Assert.False(NmapArgumentBuilder.TryBuild("  ", profile, null, out _, out var err));
        Assert.Contains("empty", err!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ShellMetacharacters_Rejected()
    {
        var profile = new ScanProfile { IncludeHostDiscovery = true };
        Assert.False(NmapArgumentBuilder.TryBuild("10.0.0.1;rm -rf", profile, null, out _, out _));
        Assert.False(NmapArgumentBuilder.TryBuild("10.0.0.1|whoami", profile, null, out _, out _));
        Assert.False(NmapArgumentBuilder.TryBuild("$(reboot)", profile, null, out _, out _));
    }

    [Fact]
    public void VulnerabilityFlag_DoesNotAddScripts()
    {
        var profile = new ScanProfile
        {
            IncludePortScanning = true,
            IncludeVulnerabilityAssessment = true,
            IncludeCorrelation = true
        };
        Assert.True(NmapArgumentBuilder.TryBuild("10.0.0.1", profile, null, out var args, out _));
        Assert.DoesNotContain(args, a => a.Contains("script", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("--script", args);
    }

    [Theory]
    [InlineData("10.0.0.1")]
    [InlineData("192.168.0.0/24")]
    [InlineData("scanme.nmap.org")]
    public void AcceptableTargets_Pass(string target)
    {
        Assert.True(NmapArgumentBuilder.IsAcceptableTarget(target));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a host!!!")]
    [InlineData("a b")]
    public void UnacceptableTargets_Fail(string target)
    {
        Assert.False(NmapArgumentBuilder.IsAcceptableTarget(target));
    }
}
