using ANPT.Application.Analysis;
using ANPT.Application.Models.Findings;
using ANPT.Domain.Entities;
using ANPT.Domain.Enums;

namespace ANPT.Tests.Application;

public class FindingEnrichmentTests
{
    private static Host MakeHost(string? ipv4 = "192.168.1.20", string? hostname = null, string? mac = null)
    {
        return new Host
        {
            Id = Guid.NewGuid(),
            ScanId = Guid.NewGuid(),
            IpAddress = ipv4 ?? string.Empty,
            Hostname = hostname,
            MacAddress = mac
        };
    }

    private static Service MakeService(Host host, int port, string state = "open", string? name = null, string? product = null)
    {
        return new Service
        {
            Id = Guid.NewGuid(),
            HostId = host.Id,
            Port = port,
            Protocol = "tcp",
            State = state,
            ServiceName = name,
            Product = product
        };
    }

    [Fact]
    public void OpenServiceExposure_ProducesTitleEvidenceRecommendation()
    {
        var scan = new Scan { Id = Guid.NewGuid(), Name = "t", Status = ScanStatus.Completed };
        var host = MakeHost();
        var svc = MakeService(host, 80, name: "http");
        host.Services.Add(svc);

        var findings = new OpenServiceExposureRule().Analyze(scan, new[] { host });
        Assert.Single(findings);
        var f = findings[0];
        Assert.Equal("OPEN-SERVICE-EXPOSURE", f.RuleId);
        Assert.Contains("80", f.Title);
        Assert.False(string.IsNullOrWhiteSpace(f.Evidence));
        Assert.False(string.IsNullOrWhiteSpace(f.Recommendation));
        Assert.Equal(Severity.Info, f.Severity);
        Assert.Contains("not a confirmed vulnerability", f.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ServiceWithoutIdentification_FlagsUnknownOpenPort()
    {
        var scan = new Scan { Id = Guid.NewGuid(), Status = ScanStatus.Completed };
        var host = MakeHost();
        host.Services.Add(MakeService(host, 8080, name: null));

        var findings = new ServiceWithoutIdentificationRule().Analyze(scan, new[] { host });
        Assert.Single(findings);
        Assert.Equal("SERVICE-WITHOUT-IDENTIFICATION", findings[0].RuleId);
        Assert.Contains("8080", findings[0].Evidence);
    }

    [Fact]
    public void ServiceWithoutIdentification_SkipsNamedService()
    {
        var scan = new Scan { Id = Guid.NewGuid(), Status = ScanStatus.Completed };
        var host = MakeHost();
        host.Services.Add(MakeService(host, 22, name: "ssh"));

        var findings = new ServiceWithoutIdentificationRule().Analyze(scan, new[] { host });
        Assert.Empty(findings);
    }

    [Fact]
    public void SensitiveService_FlagsTelnet()
    {
        var scan = new Scan { Id = Guid.NewGuid(), Status = ScanStatus.Completed };
        var host = MakeHost();
        host.Services.Add(MakeService(host, 23, name: "telnet"));

        var findings = new SensitiveServiceExposureRule().Analyze(scan, new[] { host });
        Assert.NotEmpty(findings);
        Assert.All(findings, f => Assert.Equal("SENSITIVE-SERVICE-EXPOSURE", f.RuleId));
        Assert.Contains(findings, f => f.Severity >= Severity.Medium);
        Assert.All(findings, f => Assert.False(string.IsNullOrWhiteSpace(f.Recommendation)));
    }

    [Fact]
    public void ClosedPorts_IgnoredByOpenServiceRule()
    {
        var scan = new Scan { Id = Guid.NewGuid(), Status = ScanStatus.Completed };
        var host = MakeHost();
        host.Services.Add(MakeService(host, 80, state: "closed", name: "http"));

        Assert.Empty(new OpenServiceExposureRule().Analyze(scan, new[] { host }));
    }

    [Fact]
    public void Ipv6Host_SupportedInEvidence()
    {
        var scan = new Scan { Id = Guid.NewGuid(), Status = ScanStatus.Completed };
        var host = MakeHost(ipv4: "2001:db8::1");
        host.Services.Add(MakeService(host, 443, name: "https"));

        var findings = new OpenServiceExposureRule().Analyze(scan, new[] { host });
        Assert.Single(findings);
        Assert.Contains("2001:db8::1", findings[0].Evidence);
    }

    [Fact]
    public void MacOnlyHost_Supported()
    {
        var scan = new Scan { Id = Guid.NewGuid(), Status = ScanStatus.Completed };
        var host = MakeHost(ipv4: "", mac: "AA:BB:CC:DD:EE:FF");
        host.Services.Add(MakeService(host, 445, name: "microsoft-ds"));

        var findings = new OpenServiceExposureRule().Analyze(scan, new[] { host });
        Assert.Single(findings);
        Assert.Contains("AA:BB:CC:DD:EE:FF", findings[0].Title + findings[0].Evidence);
    }

    [Fact]
    public void Rules_ExposeCategoryMetadata()
    {
        Assert.Equal("Network Exposure", new OpenServiceExposureRule().Category);
        Assert.Equal("Service Identification", new ServiceWithoutIdentificationRule().Category);
        Assert.Equal("Sensitive Services", new SensitiveServiceExposureRule().Category);
    }

    [Theory]
    [InlineData(Severity.Info, FindingPriority.Routine)]
    [InlineData(Severity.Low, FindingPriority.Routine)]
    [InlineData(Severity.Medium, FindingPriority.Elevated)]
    [InlineData(Severity.High, FindingPriority.High)]
    [InlineData(Severity.Critical, FindingPriority.Urgent)]
    public void RiskContext_MapsSeverityToPriority(Severity severity, FindingPriority expected)
    {
        var ctx = FindingRiskContext.FromSeverity(severity);
        Assert.Equal(expected, ctx.Priority);
        Assert.False(string.IsNullOrWhiteSpace(ctx.RiskLabel));
        Assert.False(string.IsNullOrWhiteSpace(ctx.Rationale));
        Assert.DoesNotContain("CVSS", ctx.Rationale, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CVE-", ctx.Rationale, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RiskContext_DoesNotInventCvssOrCve()
    {
        foreach (Severity s in Enum.GetValues<Severity>())
        {
            var ctx = FindingRiskContext.FromSeverity(s, "OPEN-SERVICE-EXPOSURE");
            Assert.DoesNotContain("CVSS", ctx.RiskLabel + ctx.Rationale, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("CVE-", ctx.RiskLabel + ctx.Rationale, StringComparison.OrdinalIgnoreCase);
        }
    }
}
