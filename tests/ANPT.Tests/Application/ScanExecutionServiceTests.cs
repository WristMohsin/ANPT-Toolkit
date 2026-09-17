using ANPT.Application.Interfaces;
using ANPT.Application.Services;
using ANPT.Domain.Entities;
using ANPT.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;

namespace ANPT.Tests.Application;

public class ScanExecutionServiceTests
{
    private readonly Guid _profileId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly Guid _authTargetId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly Guid _unauthTargetId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private readonly Guid _archivedTargetId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private readonly Guid _disabledTargetId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    private ScanExecutionService CreateSut(
        InMemoryScanRepository? scans = null,
        FakeNmapRunner? nmap = null,
        InMemoryScanProfileRepository? profiles = null,
        InMemoryTargetService? targets = null,
        FakeScanProcessTracker? tracker = null,
        FakeScanOutputPathService? paths = null,
        INmapXmlResultReader? xmlReader = null,
        INmapResultPersistenceService? persistence = null)
    {
        scans ??= new InMemoryScanRepository();
        nmap ??= new FakeNmapRunner { Available = true };
        profiles ??= new InMemoryScanProfileRepository(_profileId);
        targets ??= CreateDefaultTargets();
        tracker ??= new FakeScanProcessTracker();
        paths ??= new FakeScanOutputPathService();
        xmlReader ??= new NoOpXmlResultReader();
        persistence ??= new NoOpPersistenceService();
        return new ScanExecutionService(
            scans, profiles, targets, nmap, tracker, paths, xmlReader, persistence,
            NullLogger<ScanExecutionService>.Instance);
    }

    private InMemoryTargetService CreateDefaultTargets()
    {
        var svc = new InMemoryTargetService();
        svc.Add(new Target { Id = _authTargetId, Name = "Lab", Address = "10.0.0.1", AuthorizationConfirmed = true, Status = TargetStatus.Active });
        svc.Add(new Target { Id = _unauthTargetId, Name = "Unconfirmed", Address = "10.0.0.2", AuthorizationConfirmed = false, Status = TargetStatus.Active });
        svc.Add(new Target { Id = _archivedTargetId, Name = "Old", Address = "10.0.0.3", AuthorizationConfirmed = true, Status = TargetStatus.Archived });
        svc.Add(new Target { Id = _disabledTargetId, Name = "Off", Address = "10.0.0.4", AuthorizationConfirmed = true, Status = TargetStatus.Disabled });
        return svc;
    }

    private async Task<Scan> SeedQueuedScanAsync(InMemoryScanRepository scans, Guid targetId)
    {
        var scan = new Scan { TargetId = targetId, ScanProfileId = _profileId, Name = "test", Status = ScanStatus.Queued, CurrentStage = ScanStage.Initializing };
        return await scans.AddAsync(scan);
    }

    [Fact]
    public async Task Start_AuthorizedQueued_InvokesNmapAndCompletes()
    {
        var scans = new InMemoryScanRepository();
        var nmap = new FakeNmapRunner { Available = true, ExitCode = 0 };
        var paths = new FakeScanOutputPathService();
        var scan = await SeedQueuedScanAsync(scans, _authTargetId);
        var result = await CreateSut(scans, nmap, paths: paths).StartAsync(scan.Id);
        Assert.True(result.Succeeded);
        Assert.Equal(1, nmap.RunCallCount);
        var final = await scans.GetByIdAsync(scan.Id);
        Assert.Equal(ScanStatus.Completed, final!.Status);
        Assert.NotNull(final.OutputFilePath);
        Assert.NotNull(final.StartedAt);
        Assert.NotNull(final.CompletedAt);
    }

    [Fact]
    public async Task Start_Unauthorized_DoesNotInvokeNmap()
    {
        var scans = new InMemoryScanRepository();
        var nmap = new FakeNmapRunner { Available = true };
        var scan = await SeedQueuedScanAsync(scans, _unauthTargetId);
        Assert.False((await CreateSut(scans, nmap).StartAsync(scan.Id)).Succeeded);
        Assert.Equal(0, nmap.RunCallCount);
        Assert.Equal(ScanStatus.Failed, (await scans.GetByIdAsync(scan.Id))!.Status);
    }

    [Fact]
    public async Task Start_ArchivedTarget_DoesNotInvokeNmap()
    {
        var scans = new InMemoryScanRepository();
        var nmap = new FakeNmapRunner { Available = true };
        var scan = await SeedQueuedScanAsync(scans, _archivedTargetId);
        Assert.False((await CreateSut(scans, nmap).StartAsync(scan.Id)).Succeeded);
        Assert.Equal(0, nmap.RunCallCount);
    }

    [Fact]
    public async Task Start_DisabledTarget_DoesNotInvokeNmap()
    {
        var scans = new InMemoryScanRepository();
        var nmap = new FakeNmapRunner { Available = true };
        var scan = await SeedQueuedScanAsync(scans, _disabledTargetId);
        Assert.False((await CreateSut(scans, nmap).StartAsync(scan.Id)).Succeeded);
        Assert.Equal(0, nmap.RunCallCount);
    }

    [Fact]
    public async Task Start_MissingTarget_DoesNotInvokeNmap()
    {
        var scans = new InMemoryScanRepository();
        var nmap = new FakeNmapRunner { Available = true };
        var scan = await SeedQueuedScanAsync(scans, Guid.NewGuid());
        Assert.False((await CreateSut(scans, nmap).StartAsync(scan.Id)).Succeeded);
        Assert.Equal(0, nmap.RunCallCount);
    }

    [Fact]
    public async Task Start_DisabledProfile_DoesNotInvokeNmap()
    {
        var scans = new InMemoryScanRepository();
        var nmap = new FakeNmapRunner { Available = true };
        var profiles = new InMemoryScanProfileRepository(_profileId, enabled: false);
        var scan = await SeedQueuedScanAsync(scans, _authTargetId);
        Assert.False((await CreateSut(scans, nmap, profiles).StartAsync(scan.Id)).Succeeded);
        Assert.Equal(0, nmap.RunCallCount);
    }

    [Fact]
    public async Task Start_CompletedScan_Rejected()
    {
        var scans = new InMemoryScanRepository();
        var nmap = new FakeNmapRunner { Available = true };
        var scan = new Scan { TargetId = _authTargetId, ScanProfileId = _profileId, Name = "done", Status = ScanStatus.Completed };
        await scans.AddAsync(scan);
        Assert.False((await CreateSut(scans, nmap).StartAsync(scan.Id)).Succeeded);
        Assert.Equal(0, nmap.RunCallCount);
    }

    [Fact]
    public async Task Start_RunningScan_Rejected()
    {
        var scans = new InMemoryScanRepository();
        var nmap = new FakeNmapRunner { Available = true };
        var scan = new Scan { TargetId = _authTargetId, ScanProfileId = _profileId, Name = "run", Status = ScanStatus.Running };
        await scans.AddAsync(scan);
        Assert.False((await CreateSut(scans, nmap).StartAsync(scan.Id)).Succeeded);
        Assert.Equal(0, nmap.RunCallCount);
    }

    [Fact]
    public async Task Start_CancelledScan_Rejected()
    {
        var scans = new InMemoryScanRepository();
        var nmap = new FakeNmapRunner { Available = true };
        var scan = new Scan { TargetId = _authTargetId, ScanProfileId = _profileId, Name = "x", Status = ScanStatus.Cancelled };
        await scans.AddAsync(scan);
        Assert.False((await CreateSut(scans, nmap).StartAsync(scan.Id)).Succeeded);
        Assert.Equal(0, nmap.RunCallCount);
    }

    [Fact]
    public async Task Start_NmapUnavailable_FailsScan()
    {
        var scans = new InMemoryScanRepository();
        var nmap = new FakeNmapRunner { Available = false };
        var scan = await SeedQueuedScanAsync(scans, _authTargetId);
        Assert.False((await CreateSut(scans, nmap).StartAsync(scan.Id)).Succeeded);
        Assert.Equal(0, nmap.RunCallCount);
        Assert.Equal(ScanStatus.Failed, (await scans.GetByIdAsync(scan.Id))!.Status);
    }

    [Fact]
    public async Task Start_FailStartup_MarksFailed()
    {
        var scans = new InMemoryScanRepository();
        var nmap = new FakeNmapRunner { Available = true, FailStartup = true };
        var scan = await SeedQueuedScanAsync(scans, _authTargetId);
        Assert.False((await CreateSut(scans, nmap).StartAsync(scan.Id)).Succeeded);
        Assert.Equal(ScanStatus.Failed, (await scans.GetByIdAsync(scan.Id))!.Status);
    }

    [Fact]
    public async Task Start_NonZeroExit_MarksFailed()
    {
        var scans = new InMemoryScanRepository();
        var nmap = new FakeNmapRunner { Available = true, ExitCode = 1, Stderr = "error" };
        var scan = await SeedQueuedScanAsync(scans, _authTargetId);
        var result = await CreateSut(scans, nmap).StartAsync(scan.Id);
        Assert.True(result.Succeeded);
        Assert.Equal(ScanStatus.Failed, (await scans.GetByIdAsync(scan.Id))!.Status);
    }

    [Fact]
    public async Task Start_SimulateCancel_MarksCancelled()
    {
        var scans = new InMemoryScanRepository();
        var nmap = new FakeNmapRunner { Available = true, SimulateCancel = true };
        var scan = await SeedQueuedScanAsync(scans, _authTargetId);
        Assert.True((await CreateSut(scans, nmap).StartAsync(scan.Id)).Succeeded);
        Assert.Equal(ScanStatus.Cancelled, (await scans.GetByIdAsync(scan.Id))!.Status);
    }

    [Fact]
    public async Task Start_DuplicateRegistration_Rejected()
    {
        var scans = new InMemoryScanRepository();
        var nmap = new FakeNmapRunner { Available = true };
        var tracker = new FakeScanProcessTracker();
        var scan = await SeedQueuedScanAsync(scans, _authTargetId);
        tracker.PreRegister(scan.Id);
        Assert.False((await CreateSut(scans, nmap, tracker: tracker).StartAsync(scan.Id)).Succeeded);
        Assert.Equal(0, nmap.RunCallCount);
    }

    [Fact]
    public void RequestCancel_WhenNotRegistered_ReturnsFalse()
    {
        Assert.False(CreateSut().RequestCancel(Guid.NewGuid()));
    }

    [Fact]
    public void RequestCancel_WhenRegistered_ReturnsTrue()
    {
        var tracker = new FakeScanProcessTracker();
        var id = Guid.NewGuid();
        tracker.PreRegister(id);
        Assert.True(CreateSut(tracker: tracker).RequestCancel(id));
    }

    [Fact]
    public async Task CheckNmapAvailability_Delegates()
    {
        var nmap = new FakeNmapRunner { Available = true, VersionText = "Nmap 7.94" };
        var result = await CreateSut(nmap: nmap).CheckNmapAvailabilityAsync();
        Assert.True(result.IsAvailable);
    }

    [Fact]
    public async Task Start_UniqueXmlPaths_PerScan()
    {
        var scans = new InMemoryScanRepository();
        var nmap = new FakeNmapRunner { Available = true, ExitCode = 0 };
        var paths = new FakeScanOutputPathService();
        var scan1 = await SeedQueuedScanAsync(scans, _authTargetId);
        var scan2 = await SeedQueuedScanAsync(scans, _authTargetId);
        await CreateSut(scans, nmap, paths: paths).StartAsync(scan1.Id);
        await CreateSut(scans, nmap, paths: paths).StartAsync(scan2.Id);
        var s1 = await scans.GetByIdAsync(scan1.Id);
        var s2 = await scans.GetByIdAsync(scan2.Id);
        Assert.NotEqual(s1!.OutputFilePath, s2!.OutputFilePath);
    }

    private sealed class FakeNmapRunner : INmapProcessRunner
    {
        public bool Available { get; set; }
        public bool FailStartup { get; set; }
        public bool SimulateCancel { get; set; }
        public int ExitCode { get; set; }
        public string Stderr { get; set; } = string.Empty;
        public string? VersionText { get; set; }
        public int RunCallCount { get; private set; }
        public NmapRunRequest? LastRequest { get; private set; }

        public Task<NmapAvailabilityResult> CheckAvailabilityAsync(CancellationToken cancellationToken = default)
        {
            if (!Available)
                return Task.FromResult(NmapAvailabilityResult.Unavailable("Nmap not found (fake)."));
            return Task.FromResult(NmapAvailabilityResult.Available(@"C:\Program Files\Nmap\nmap.exe", VersionText));
        }

        public Task<NmapRunResult> RunAsync(NmapRunRequest request, CancellationToken cancellationToken = default)
        {
            RunCallCount++;
            LastRequest = request;
            if (FailStartup)
                return Task.FromResult(NmapRunResult.StartupFailure("start failed (fake)"));
            if (SimulateCancel)
                return Task.FromResult(NmapRunResult.FromProcess(-1, "", "", TimeSpan.FromMilliseconds(10), cancelled: true, request.XmlOutputPath));
            return Task.FromResult(NmapRunResult.FromProcess(ExitCode, "ok", Stderr, TimeSpan.FromMilliseconds(5), false, request.XmlOutputPath));
        }
    }

    private sealed class FakeScanProcessTracker : IScanProcessTracker
    {
        private readonly Dictionary<Guid, CancellationTokenSource> _map = new();
        public void PreRegister(Guid id) => _map[id] = new CancellationTokenSource();
        public bool TryRegister(Guid scanId, CancellationTokenSource linkedCts)
        {
            if (_map.ContainsKey(scanId)) return false;
            _map[scanId] = linkedCts;
            return true;
        }
        public bool TryCancel(Guid scanId)
        {
            if (!_map.TryGetValue(scanId, out var cts)) return false;
            cts.Cancel();
            return true;
        }
        public void Unregister(Guid scanId) => _map.Remove(scanId);
        public bool IsRegistered(Guid scanId) => _map.ContainsKey(scanId);
    }

    private sealed class FakeScanOutputPathService : IScanOutputPathService
    {
        private int _n;
        public void EnsureOutputDirectory() { }
        public string CreateUniqueXmlPath(Guid scanId)
        {
            _n++;
            return $"C:\\app\\ScanOutput\\scan-{scanId:N}-{_n}.xml";
        }
    }

    private sealed class InMemoryScanRepository : IScanRepository
    {
        private readonly List<Scan> _items = new();
        public Task<IReadOnlyList<Scan>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Scan>>(_items.ToList());
        public Task<IReadOnlyList<Scan>> SearchAsync(string? searchText, ScanStatus? statusFilter, Guid? targetId, Guid? profileId, CancellationToken cancellationToken = default) => GetAllAsync(cancellationToken);
        public Task<Scan?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(_items.FirstOrDefault(s => s.Id == id));
        public Task<int> CountAsync(CancellationToken cancellationToken = default) => Task.FromResult(_items.Count);
        public Task<int> CountActiveAsync(CancellationToken cancellationToken = default) => Task.FromResult(_items.Count(s => s.Status is ScanStatus.Queued or ScanStatus.Running));
        public Task<Scan> AddAsync(Scan scan, CancellationToken cancellationToken = default)
        {
            if (scan.Id == Guid.Empty) scan.Id = Guid.NewGuid();
            _items.Add(scan);
            return Task.FromResult(scan);
        }
        public Task UpdateAsync(Scan scan, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class InMemoryScanProfileRepository : IScanProfileRepository
    {
        private readonly ScanProfile _profile;
        public InMemoryScanProfileRepository(Guid id, bool enabled = true) =>
            _profile = new ScanProfile { Id = id, Name = "Discovery", IsEnabled = enabled, IncludeHostDiscovery = true };
        public Task<IReadOnlyList<ScanProfile>> GetEnabledAsync(CancellationToken cancellationToken = default)
        {
            IReadOnlyList<ScanProfile> list = _profile.IsEnabled ? new[] { _profile } : Array.Empty<ScanProfile>();
            return Task.FromResult(list);
        }
        public Task<ScanProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(id == _profile.Id ? _profile : null);
    }

    private sealed class InMemoryTargetService : ITargetService
    {
        private readonly List<Target> _items = new();
        public void Add(Target t) => _items.Add(t);
        public Task<IReadOnlyList<Target>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Target>>(_items.ToList());
        public Task<Target?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(_items.FirstOrDefault(t => t.Id == id));
        public Task<IReadOnlyList<Target>> SearchAsync(string? searchText, TargetStatus? statusFilter, CancellationToken cancellationToken = default) => GetAllAsync(cancellationToken);
        public Task<int> GetTotalCountAsync(CancellationToken cancellationToken = default) => Task.FromResult(_items.Count);
        public Task<TargetServiceResult> CreateAsync(CreateTargetRequest request, string? createdBy, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<TargetServiceResult> UpdateAsync(UpdateTargetRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<TargetServiceResult> ArchiveAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<TargetServiceResult> SetAuthorizationAsync(Guid id, bool confirmed, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public bool IsEligibleForScan(Target target) => target is not null && target.Status == TargetStatus.Active && target.AuthorizationConfirmed;
    }

    private sealed class NoOpXmlResultReader : INmapXmlResultReader
    {
        public Task<ANPT.Application.Models.Nmap.NmapParseResult> ReadAsync(
            string xmlFilePath, CancellationToken cancellationToken = default) =>
            Task.FromResult(ANPT.Application.Models.Nmap.NmapParseResult.Failure("No-op reader in unit tests."));

        public Task<ANPT.Application.Models.Nmap.NmapParseResult> ReadForScanAsync(
            Scan scan, CancellationToken cancellationToken = default) =>
            Task.FromResult(ANPT.Application.Models.Nmap.NmapParseResult.Failure("No-op reader in unit tests."));
    }

    private sealed class NoOpPersistenceService : INmapResultPersistenceService
    {
        public Task<NmapPersistenceResult> PersistAsync(
            Guid scanId,
            ANPT.Application.Models.Nmap.NmapScanResult parsedResult,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(NmapPersistenceResult.Success(0, 0));
    }
}
