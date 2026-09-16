using ANPT.Domain.Entities;
using ANPT.Domain.Enums;

namespace ANPT.Tests.Domain;

public class EntityTests
{
    [Fact]
    public void BaseEntity_HasDefaultIdAndCreatedAt()
    {
        var entity = new Target();
        Assert.NotEqual(Guid.Empty, entity.Id);
        Assert.True(entity.CreatedAt <= DateTime.UtcNow);
        Assert.True(entity.CreatedAt > DateTime.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public void Target_Defaults()
    {
        var target = new Target
        {
            Name = "Lab Network",
            Address = "192.168.1.0/24",
            TargetType = "Network"
        };

        Assert.Equal(TargetStatus.Active, target.Status);
        Assert.False(target.AuthorizationConfirmed);
        Assert.Empty(target.Scans);
    }

    [Fact]
    public void Scan_Defaults()
    {
        var scan = new Scan();
        Assert.Equal(ScanStatus.Queued, scan.Status);
        Assert.Equal(ScanStage.Initializing, scan.CurrentStage);
        Assert.Equal(0, scan.ProgressPercent);
    }

    [Fact]
    public void Finding_SeverityAndStatusDefaults()
    {
        var finding = new Finding { Title = "Test" };
        Assert.Equal(Severity.Info, finding.Severity);
        Assert.Equal(FindingStatus.Detected, finding.Status);
    }

    [Fact]
    public void ScanProfile_BuiltInFlags()
    {
        var profile = new ScanProfile
        {
            Name = "Discovery",
            IncludeHostDiscovery = true,
            IsBuiltIn = true
        };
        Assert.True(profile.IsBuiltIn);
        Assert.True(profile.IsEnabled);
        Assert.True(profile.IncludeHostDiscovery);
        Assert.False(profile.IncludePortScanning);
    }
}
