using ANPT.Application.Models.Nmap;
using ANPT.Domain.Entities;
using ANPT.Domain.Enums;
using ANPT.Infrastructure.Data;
using ANPT.Infrastructure.Scanning;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ANPT.Tests.Application;

public class NmapResultPersistenceServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AnptDbContext _db;
    private readonly NmapResultPersistenceService _sut;
    private readonly Guid _profileId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public NmapResultPersistenceServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<AnptDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new AnptDbContext(options);
        _db.Database.EnsureCreated();
        _sut = new NmapResultPersistenceService(_db, NullLogger<NmapResultPersistenceService>.Instance);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private async Task<Scan> SeedScanAsync()
    {
        var target = new Target
        {
            Id = Guid.NewGuid(),
            Name = "Lab",
            Address = "10.0.0.1",
            AuthorizationConfirmed = true,
            Status = TargetStatus.Active
        };
        _db.Targets.Add(target);

        var scan = new Scan
        {
            Id = Guid.NewGuid(),
            TargetId = target.Id,
            ScanProfileId = _profileId,
            Name = "persist-test",
            Status = ScanStatus.Completed,
            CurrentStage = ScanStage.Completed
        };
        _db.Scans.Add(scan);
        await _db.SaveChangesAsync();
        return scan;
    }

    private static NmapScanResult BuildFullResult() => new()
    {
        Scanner = "nmap",
        Version = "7.94",
        Arguments = "nmap -sT -sV 10.0.0.1",
        ElapsedSeconds = 12.5,
        Summary = "Nmap done; 2 hosts up",
        ExitStatus = "success",
        Hosts = new[]
        {
            new NmapHostResult
            {
                State = "up",
                StateReason = "syn-ack",
                Addresses = new[]
                {
                    new NmapAddressResult { Address = "10.0.0.1", AddressType = "ipv4" },
                    new NmapAddressResult { Address = "00:11:22:33:44:55", AddressType = "mac", Vendor = "VendorCo" },
                    new NmapAddressResult { Address = "2001:db8::1", AddressType = "ipv6" }
                },
                Hostnames = new[]
                {
                    new NmapHostnameResult { Name = "lab.local", Type = "PTR" },
                    new NmapHostnameResult { Name = "lab", Type = "user" }
                },
                Ports = new[]
                {
                    new NmapPortResult
                    {
                        Protocol = "tcp", PortId = 22, State = "open", Reason = "syn-ack",
                        Service = new NmapServiceResult
                        {
                            Name = "ssh", Product = "OpenSSH", Version = "8.9p1",
                            ExtraInfo = "Ubuntu", Method = "probed", Confidence = 10
                        }
                    },
                    new NmapPortResult
                    {
                        Protocol = "tcp", PortId = 80, State = "open", Reason = "syn-ack",
                        Service = new NmapServiceResult
                        {
                            Name = "http", Product = "nginx", Version = "1.18.0",
                            Tunnel = "ssl", Method = "probed", Confidence = 10
                        }
                    },
                    new NmapPortResult
                    {
                        Protocol = "tcp", PortId = 443, State = "filtered", Reason = "no-response"
                    }
                }
            },
            new NmapHostResult
            {
                State = "up",
                StateReason = "echo-reply",
                Addresses = new[]
                {
                    new NmapAddressResult { Address = "10.0.0.2", AddressType = "ipv4" }
                },
                Ports = new[]
                {
                    new NmapPortResult
                    {
                        Protocol = "tcp", PortId = 22, State = "open", Reason = "syn-ack",
                        Service = new NmapServiceResult { Name = "ssh" }
                    }
                }
            }
        }
    };

    [Fact]
    public async Task Persist_FullGraph_Succeeds()
    {
        var scan = await SeedScanAsync();
        var result = await _sut.PersistAsync(scan.Id, BuildFullResult());

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.HostCount);
        Assert.Equal(4, result.PortCount);

        var hosts = await _db.Hosts.Where(h => h.ScanId == scan.Id)
            .Include(h => h.Addresses)
            .Include(h => h.Hostnames)
            .Include(h => h.Services)
            .ToListAsync();

        Assert.Equal(2, hosts.Count);

        var h1 = hosts.Single(h => h.IpAddress == "10.0.0.1");
        Assert.Equal("up", h1.Status);
        Assert.Equal("syn-ack", h1.StateReason);
        Assert.Equal("00:11:22:33:44:55", h1.MacAddress);
        Assert.Equal("lab.local", h1.Hostname);
        Assert.Equal(3, h1.Addresses.Count);
        Assert.Contains(h1.Addresses, a => a.AddressType == "ipv4" && a.Address == "10.0.0.1");
        Assert.Contains(h1.Addresses, a => a.AddressType == "mac" && a.Vendor == "VendorCo");
        Assert.Contains(h1.Addresses, a => a.AddressType == "ipv6");
        Assert.Equal(2, h1.Hostnames.Count);
        Assert.Equal(3, h1.Services.Count);

        var ssh = h1.Services.Single(s => s.Port == 22);
        Assert.Equal("ssh", ssh.ServiceName);
        Assert.Equal("OpenSSH", ssh.Product);
        Assert.Equal("8.9p1", ssh.Version);
        Assert.Equal("Ubuntu", ssh.Banner);
        Assert.Equal(10, ssh.Confidence);

        var filtered = h1.Services.Single(s => s.Port == 443);
        Assert.Equal("filtered", filtered.State);
        Assert.Null(filtered.ServiceName);

        var refreshed = await _db.Scans.AsNoTracking().FirstAsync(s => s.Id == scan.Id);
        Assert.Equal("nmap", refreshed.NmapScanner);
        Assert.Equal("7.94", refreshed.NmapVersion);
        Assert.Equal(12.5, refreshed.NmapElapsedSeconds);
        Assert.Equal("success", refreshed.NmapExitStatus);
    }

    [Fact]
    public async Task Persist_IPv6OnlyHost_Succeeds()
    {
        var scan = await SeedScanAsync();
        var parsed = new NmapScanResult
        {
            Hosts = new[]
            {
                new NmapHostResult
                {
                    State = "up",
                    Addresses = new[]
                    {
                        new NmapAddressResult { Address = "2001:db8::99", AddressType = "ipv6" }
                    }
                }
            }
        };

        var result = await _sut.PersistAsync(scan.Id, parsed);
        Assert.True(result.Succeeded);
        var host = await _db.Hosts.Include(h => h.Addresses).SingleAsync(h => h.ScanId == scan.Id);
        Assert.Equal("2001:db8::99", host.IpAddress);
        Assert.Single(host.Addresses);
    }

    [Fact]
    public async Task Persist_MacOnlyHost_Succeeds()
    {
        var scan = await SeedScanAsync();
        var parsed = new NmapScanResult
        {
            Hosts = new[]
            {
                new NmapHostResult
                {
                    State = "up",
                    Addresses = new[]
                    {
                        new NmapAddressResult { Address = "aa:bb:cc:dd:ee:ff", AddressType = "mac", Vendor = "X" }
                    }
                }
            }
        };

        var result = await _sut.PersistAsync(scan.Id, parsed);
        Assert.True(result.Succeeded);
        var host = await _db.Hosts.Include(h => h.Addresses).SingleAsync(h => h.ScanId == scan.Id);
        Assert.Equal(string.Empty, host.IpAddress);
        Assert.Equal("aa:bb:cc:dd:ee:ff", host.MacAddress);
        Assert.Single(host.Addresses);
    }

    [Fact]
    public async Task Persist_PortWithoutService_Succeeds()
    {
        var scan = await SeedScanAsync();
        var parsed = new NmapScanResult
        {
            Hosts = new[]
            {
                new NmapHostResult
                {
                    State = "up",
                    Addresses = new[] { new NmapAddressResult { Address = "10.0.0.9", AddressType = "ipv4" } },
                    Ports = new[]
                    {
                        new NmapPortResult { Protocol = "tcp", PortId = 1234, State = "closed", Reason = "reset" }
                    }
                }
            }
        };

        var result = await _sut.PersistAsync(scan.Id, parsed);
        Assert.True(result.Succeeded);
        var svc = await _db.Services.SingleAsync();
        Assert.Equal(1234, svc.Port);
        Assert.Null(svc.ServiceName);
        Assert.Equal("closed", svc.State);
    }

    [Fact]
    public async Task Persist_Reprocess_ReplacesPreviousResults()
    {
        var scan = await SeedScanAsync();
        var first = new NmapScanResult
        {
            Hosts = new[]
            {
                new NmapHostResult
                {
                    State = "up",
                    Addresses = new[] { new NmapAddressResult { Address = "10.0.0.1", AddressType = "ipv4" } },
                    Ports = new[]
                    {
                        new NmapPortResult { Protocol = "tcp", PortId = 22, State = "open" }
                    }
                }
            }
        };
        Assert.True((await _sut.PersistAsync(scan.Id, first)).Succeeded);
        Assert.Equal(1, await _db.Hosts.CountAsync(h => h.ScanId == scan.Id));
        Assert.Equal(1, await _db.Services.CountAsync());

        var second = new NmapScanResult
        {
            Scanner = "nmap",
            Version = "7.95",
            Hosts = new[]
            {
                new NmapHostResult
                {
                    State = "up",
                    Addresses = new[] { new NmapAddressResult { Address = "10.0.0.5", AddressType = "ipv4" } },
                    Ports = new[]
                    {
                        new NmapPortResult { Protocol = "tcp", PortId = 80, State = "open", Service = new NmapServiceResult { Name = "http" } },
                        new NmapPortResult { Protocol = "tcp", PortId = 443, State = "open" }
                    }
                },
                new NmapHostResult
                {
                    State = "up",
                    Addresses = new[] { new NmapAddressResult { Address = "10.0.0.6", AddressType = "ipv4" } }
                }
            }
        };

        var result = await _sut.PersistAsync(scan.Id, second);
        Assert.True(result.Succeeded);
        Assert.Equal(2, result.HostCount);
        Assert.Equal(2, result.PortCount);
        Assert.Equal(2, await _db.Hosts.CountAsync(h => h.ScanId == scan.Id));
        Assert.Equal(2, await _db.Services.CountAsync());
        Assert.DoesNotContain(await _db.Hosts.ToListAsync(), h => h.IpAddress == "10.0.0.1");

        var refreshed = await _db.Scans.AsNoTracking().FirstAsync(s => s.Id == scan.Id);
        Assert.Equal("7.95", refreshed.NmapVersion);
    }

    [Fact]
    public async Task Persist_UnknownScan_Fails()
    {
        var result = await _sut.PersistAsync(Guid.NewGuid(), new NmapScanResult());
        Assert.False(result.Succeeded);
        Assert.Contains("not found", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Persist_EmptyScanId_Fails()
    {
        var result = await _sut.PersistAsync(Guid.Empty, new NmapScanResult());
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Persist_NullResult_Fails()
    {
        var scan = await SeedScanAsync();
        var result = await _sut.PersistAsync(scan.Id, null!);
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Persist_EmptyHosts_SucceedsWithMetadata()
    {
        var scan = await SeedScanAsync();
        var parsed = new NmapScanResult
        {
            Scanner = "nmap",
            Version = "7.94",
            Summary = "0 hosts up",
            ExitStatus = "success",
            Hosts = Array.Empty<NmapHostResult>()
        };

        var result = await _sut.PersistAsync(scan.Id, parsed);
        Assert.True(result.Succeeded);
        Assert.Equal(0, result.HostCount);
        Assert.Equal(0, await _db.Hosts.CountAsync(h => h.ScanId == scan.Id));

        var refreshed = await _db.Scans.AsNoTracking().FirstAsync(s => s.Id == scan.Id);
        Assert.Equal("nmap", refreshed.NmapScanner);
        Assert.Equal("0 hosts up", refreshed.NmapSummary);
    }

    [Fact]
    public async Task Persist_DoesNotAffectOtherScans()
    {
        var scan1 = await SeedScanAsync();
        var scan2 = await SeedScanAsync();

        await _sut.PersistAsync(scan1.Id, new NmapScanResult
        {
            Hosts = new[]
            {
                new NmapHostResult
                {
                    State = "up",
                    Addresses = new[] { new NmapAddressResult { Address = "10.0.0.1", AddressType = "ipv4" } }
                }
            }
        });

        await _sut.PersistAsync(scan2.Id, new NmapScanResult
        {
            Hosts = new[]
            {
                new NmapHostResult
                {
                    State = "up",
                    Addresses = new[] { new NmapAddressResult { Address = "10.0.0.2", AddressType = "ipv4" } }
                }
            }
        });

        Assert.Equal(1, await _db.Hosts.CountAsync(h => h.ScanId == scan1.Id));
        Assert.Equal(1, await _db.Hosts.CountAsync(h => h.ScanId == scan2.Id));

        await _sut.PersistAsync(scan1.Id, new NmapScanResult
        {
            Hosts = new[]
            {
                new NmapHostResult
                {
                    State = "up",
                    Addresses = new[] { new NmapAddressResult { Address = "10.0.0.9", AddressType = "ipv4" } }
                }
            }
        });

        Assert.Equal(1, await _db.Hosts.CountAsync(h => h.ScanId == scan1.Id));
        Assert.Equal("10.0.0.9", (await _db.Hosts.SingleAsync(h => h.ScanId == scan1.Id)).IpAddress);
        Assert.Equal("10.0.0.2", (await _db.Hosts.SingleAsync(h => h.ScanId == scan2.Id)).IpAddress);
    }
}
