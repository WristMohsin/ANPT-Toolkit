using ANPT.Application.Analysis;
using ANPT.Domain.Entities;
using ANPT.Domain.Enums;

namespace ANPT.Tests.Application;

public class AnalysisRulesTests
{
    private static Scan CompletedScan() => new()
    {
        Id = Guid.NewGuid(),
        Name = "t",
        Status = ScanStatus.Completed,
        TargetId = Guid.NewGuid(),
        ScanProfileId = Guid.NewGuid()
    };

    private static Host HostWithServices(params Service[] services)
    {
        var h = new Host
        {
            Id = Guid.NewGuid(),
            IpAddress = "10.0.0.5",
            Status = "up"
        };
        foreach (var s in services)
        {
            s.HostId = h.Id;
            h.Services.Add(s);
        }
        return h;
    }

    [Fact]
    public void OpenServiceExposure_ProducesFinding_ForOpenPort()
    {
        var rule = new OpenServiceExposureRule();
        var scan = CompletedScan();
        var host = HostWithServices(new Service
        {
            Id = Guid.NewGuid(),
            Port = 80,
            Protocol = "tcp",
            State = "open",
            ServiceName = "http"
        });

        var findings = rule.Analyze(scan, new[] { host });
        Assert.Single(findings);
        Assert.Equal(rule.RuleId, findings[0].RuleId);
        Assert.Equal(Severity.Info, findings[0].Severity);
        Assert.Equal(scan.Id, findings[0].ScanId);
        Assert.Equal(host.Id, findings[0].HostId);
        Assert.Contains("80", findings[0].Title);
    }

    [Fact]
    public void OpenServiceExposure_IgnoresClosedPorts()
    {
        var rule = new OpenServiceExposureRule();
        var host = HostWithServices(new Service
        {
            Id = Guid.NewGuid(),
            Port = 80,
            Protocol = "tcp",
            State = "closed"
        });

        Assert.Empty(rule.Analyze(CompletedScan(), new[] { host }));
    }

    [Fact]
    public void ServiceWithoutIdentification_FlagsOpenUnknownService()
    {
        var rule = new ServiceWithoutIdentificationRule();
        var host = HostWithServices(new Service
        {
            Id = Guid.NewGuid(),
            Port = 1234,
            Protocol = "tcp",
            State = "open",
            ServiceName = null
        });

        var findings = rule.Analyze(CompletedScan(), new[] { host });
        Assert.Single(findings);
        Assert.Equal(Severity.Low, findings[0].Severity);
        Assert.Equal(rule.RuleId, findings[0].RuleId);
    }

    [Fact]
    public void ServiceWithoutIdentification_SkipsNamedServices()
    {
        var rule = new ServiceWithoutIdentificationRule();
        var host = HostWithServices(new Service
        {
            Id = Guid.NewGuid(),
            Port = 22,
            Protocol = "tcp",
            State = "open",
            ServiceName = "ssh"
        });

        Assert.Empty(rule.Analyze(CompletedScan(), new[] { host }));
    }

    [Fact]
    public void SensitiveService_FlagsTelnet()
    {
        var rule = new SensitiveServiceExposureRule();
        var host = HostWithServices(new Service
        {
            Id = Guid.NewGuid(),
            Port = 23,
            Protocol = "tcp",
            State = "open",
            ServiceName = "telnet"
        });

        var findings = rule.Analyze(CompletedScan(), new[] { host });
        Assert.Single(findings);
        Assert.Equal(Severity.Medium, findings[0].Severity);
        Assert.Contains("Telnet", findings[0].Title, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("vulnerable", findings[0].Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SensitiveService_FlagsByServiceName()
    {
        var rule = new SensitiveServiceExposureRule();
        var host = HostWithServices(new Service
        {
            Id = Guid.NewGuid(),
            Port = 9999,
            Protocol = "tcp",
            State = "open",
            ServiceName = "mysql"
        });

        var findings = rule.Analyze(CompletedScan(), new[] { host });
        Assert.Single(findings);
        Assert.Equal(Severity.Medium, findings[0].Severity);
    }

    [Fact]
    public void SensitiveService_IgnoresNonSensitive()
    {
        var rule = new SensitiveServiceExposureRule();
        var host = HostWithServices(new Service
        {
            Id = Guid.NewGuid(),
            Port = 80,
            Protocol = "tcp",
            State = "open",
            ServiceName = "http"
        });

        Assert.Empty(rule.Analyze(CompletedScan(), new[] { host }));
    }

    [Fact]
    public void OpenService_HandlesIpv6AndMacOnlyHosts()
    {
        var rule = new OpenServiceExposureRule();
        var ipv6 = new Host
        {
            Id = Guid.NewGuid(),
            IpAddress = "2001:db8::1",
            Services =
            {
                new Service { Id = Guid.NewGuid(), Port = 22, Protocol = "tcp", State = "open", ServiceName = "ssh" }
            }
        };
        var macOnly = new Host
        {
            Id = Guid.NewGuid(),
            IpAddress = "",
            MacAddress = "aa:bb:cc:dd:ee:ff",
            Services =
            {
                new Service { Id = Guid.NewGuid(), Port = 80, Protocol = "tcp", State = "open" }
            }
        };

        var findings = rule.Analyze(CompletedScan(), new[] { ipv6, macOnly });
        Assert.Equal(2, findings.Count);
        Assert.Contains(findings, f => f.Title.Contains("2001:db8::1"));
        Assert.Contains(findings, f => f.Title.Contains("aa:bb:cc:dd:ee:ff"));
    }
}
