using ANPT.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ANPT.Infrastructure.Data;

public class AnptDbContext : DbContext
{
    public AnptDbContext(DbContextOptions<AnptDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Target> Targets => Set<Target>();
    public DbSet<ScanProfile> ScanProfiles => Set<ScanProfile>();
    public DbSet<Scan> Scans => Set<Scan>();
    public DbSet<Host> Hosts => Set<Host>();
    public DbSet<HostAddress> HostAddresses => Set<HostAddress>();
    public DbSet<HostHostname> HostHostnames => Set<HostHostname>();
    public DbSet<Service> Services => Set<Service>();
    public DbSet<Finding> Findings => Set<Finding>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Username).IsUnique();
            e.Property(x => x.Username).HasMaxLength(100).IsRequired();
            e.Property(x => x.DisplayName).HasMaxLength(200);
            e.Property(x => x.PasswordHash).HasMaxLength(500);
        });

        modelBuilder.Entity<Target>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Address).HasMaxLength(100).IsRequired();
            e.Property(x => x.TargetType).HasMaxLength(50);
            e.Property(x => x.Description).HasMaxLength(1000);
            e.HasIndex(x => x.Address);
        });

        modelBuilder.Entity<ScanProfile>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.Description).HasMaxLength(500);
        });

        modelBuilder.Entity<Scan>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.StatusMessage).HasMaxLength(500);
            e.Property(x => x.ErrorMessage).HasMaxLength(2000);
            e.Property(x => x.CreatedBy).HasMaxLength(100);
            e.Property(x => x.OutputFilePath).HasMaxLength(500);
            e.Property(x => x.NmapScanner).HasMaxLength(50);
            e.Property(x => x.NmapVersion).HasMaxLength(50);
            e.Property(x => x.NmapArguments).HasMaxLength(1000);
            e.Property(x => x.NmapSummary).HasMaxLength(1000);
            e.Property(x => x.NmapExitStatus).HasMaxLength(50);
            e.HasOne(x => x.Target).WithMany(t => t.Scans).HasForeignKey(x => x.TargetId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ScanProfile).WithMany().HasForeignKey(x => x.ScanProfileId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.CreatedAt);
            e.HasIndex(x => x.TargetId);
        });

        modelBuilder.Entity<Host>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.IpAddress).HasMaxLength(45).IsRequired();
            e.Property(x => x.Hostname).HasMaxLength(255);
            e.Property(x => x.MacAddress).HasMaxLength(50);
            e.Property(x => x.OsInfo).HasMaxLength(200);
            e.Property(x => x.Status).HasMaxLength(50);
            e.Property(x => x.StateReason).HasMaxLength(100);
            e.HasOne(x => x.Scan).WithMany(s => s.Hosts).HasForeignKey(x => x.ScanId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.ScanId);
            e.HasIndex(x => x.IpAddress);
        });

        modelBuilder.Entity<HostAddress>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Address).HasMaxLength(100).IsRequired();
            e.Property(x => x.AddressType).HasMaxLength(20).IsRequired();
            e.Property(x => x.Vendor).HasMaxLength(200);
            e.HasOne(x => x.Host).WithMany(h => h.Addresses).HasForeignKey(x => x.HostId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.HostId);
            e.HasIndex(x => new { x.HostId, x.AddressType, x.Address });
        });

        modelBuilder.Entity<HostHostname>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(255).IsRequired();
            e.Property(x => x.Type).HasMaxLength(50);
            e.HasOne(x => x.Host).WithMany(h => h.Hostnames).HasForeignKey(x => x.HostId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.HostId);
        });

        modelBuilder.Entity<Service>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Protocol).HasMaxLength(10);
            e.Property(x => x.State).HasMaxLength(20);
            e.Property(x => x.StateReason).HasMaxLength(100);
            e.Property(x => x.ServiceName).HasMaxLength(100);
            e.Property(x => x.Product).HasMaxLength(200);
            e.Property(x => x.Version).HasMaxLength(100);
            e.Property(x => x.Banner).HasMaxLength(2000);
            e.Property(x => x.Tunnel).HasMaxLength(50);
            e.Property(x => x.DetectionMethod).HasMaxLength(50);
            e.HasOne(x => x.Host).WithMany(h => h.Services).HasForeignKey(x => x.HostId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.HostId);
            e.HasIndex(x => new { x.HostId, x.Port, x.Protocol });
        });

        modelBuilder.Entity<Finding>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(300).IsRequired();
            e.Property(x => x.Description).HasMaxLength(4000);
            e.Property(x => x.Evidence).HasMaxLength(4000);
            e.Property(x => x.Impact).HasMaxLength(2000);
            e.Property(x => x.Recommendation).HasMaxLength(2000);
            e.Property(x => x.Reference).HasMaxLength(500);
            e.Property(x => x.AffectedPort).HasMaxLength(20);
            e.Property(x => x.AffectedService).HasMaxLength(100);
            e.Property(x => x.RuleId).HasMaxLength(100);
            e.HasOne(x => x.Scan).WithMany(s => s.Findings).HasForeignKey(x => x.ScanId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Host).WithMany().HasForeignKey(x => x.HostId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Service).WithMany().HasForeignKey(x => x.ServiceId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(x => x.Severity);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.RuleId);
            e.HasIndex(x => new { x.ScanId, x.RuleId, x.HostId, x.ServiceId });
        });

        modelBuilder.Entity<AuditLog>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Username).HasMaxLength(100);
            e.Property(x => x.Action).HasMaxLength(100).IsRequired();
            e.Property(x => x.ResourceType).HasMaxLength(100);
            e.Property(x => x.ResourceId).HasMaxLength(100);
            e.Property(x => x.Result).HasMaxLength(50);
            e.Property(x => x.Details).HasMaxLength(2000);
            e.Property(x => x.IpAddress).HasMaxLength(45);
            e.HasIndex(x => x.CreatedAt);
        });

        modelBuilder.Entity<AppSetting>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Key).IsUnique();
            e.Property(x => x.Key).HasMaxLength(100).IsRequired();
            e.Property(x => x.Value).HasMaxLength(4000);
            e.Property(x => x.Category).HasMaxLength(50);
            e.Property(x => x.Description).HasMaxLength(500);
        });

        SeedBuiltInData(modelBuilder);
    }

    private static void SeedBuiltInData(ModelBuilder modelBuilder)
    {
        var discoveryId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var networkId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var fullId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        modelBuilder.Entity<ScanProfile>().HasData(
            new ScanProfile
            {
                Id = discoveryId,
                Name = "Discovery",
                Description = "Host discovery only",
                IncludeHostDiscovery = true,
                IncludePortScanning = false,
                IncludeServiceEnumeration = false,
                IncludeVulnerabilityAssessment = false,
                IncludeCorrelation = false,
                IsBuiltIn = true,
                IsEnabled = true,
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new ScanProfile
            {
                Id = networkId,
                Name = "Network Assessment",
                Description = "Host discovery, port scanning and service enumeration",
                IncludeHostDiscovery = true,
                IncludePortScanning = true,
                IncludeServiceEnumeration = true,
                IncludeVulnerabilityAssessment = false,
                IncludeCorrelation = false,
                IsBuiltIn = true,
                IsEnabled = true,
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new ScanProfile
            {
                Id = fullId,
                Name = "Full Assessment",
                Description = "Complete assessment including vulnerability analysis and correlation",
                IncludeHostDiscovery = true,
                IncludePortScanning = true,
                IncludeServiceEnumeration = true,
                IncludeVulnerabilityAssessment = true,
                IncludeCorrelation = true,
                IsBuiltIn = true,
                IsEnabled = true,
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        );
    }
}
