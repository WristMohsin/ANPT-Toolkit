using ANPT.Application.Interfaces;
using ANPT.Application.Services;
using ANPT.Domain.Entities;
using ANPT.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;

namespace ANPT.Tests.Application;

public class ScanServiceTests
{
    private readonly Guid _profileId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly Guid _authTargetId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly Guid _unauthTargetId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private readonly Guid _archivedTargetId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private readonly Guid _disabledTargetId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    private ScanService CreateService(
        InMemoryScanRepository? scans = null,
        InMemoryScanProfileRepository? profiles = null,
        InMemoryTargetService? targets = null)
    {
        scans ??= new InMemoryScanRepository();
        profiles ??= new InMemoryScanProfileRepository(_profileId);
        targets ??= CreateDefaultTargets();
        return new ScanService(scans, profiles, targets, NullLogger<ScanService>.Instance);
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

    [Fact]
    public async Task Create_Valid_SucceedsAsQueued()
    {
        var scans = new InMemoryScanRepository();
        var svc = CreateService(scans);
        var result = await svc.CreateAsync(new CreateScanRequest { TargetId = _authTargetId, ScanProfileId = _profileId }, "admin");
        Assert.True(result.Succeeded);
        Assert.NotNull(result.Scan);
        Assert.Equal(ScanStatus.Queued, result.Scan.Status);
        Assert.Equal(ScanStage.Initializing, result.Scan.CurrentStage);
        Assert.Equal(1, await svc.GetTotalCountAsync());
        Assert.Equal(1, await svc.GetActiveCountAsync());
    }

    [Fact]
    public async Task Create_MissingTarget_Fails()
    {
        var result = await CreateService().CreateAsync(new CreateScanRequest { TargetId = Guid.Empty, ScanProfileId = _profileId }, null);
        Assert.False(result.Succeeded);
        Assert.Contains("target", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_MissingProfile_Fails()
    {
        var result = await CreateService().CreateAsync(new CreateScanRequest { TargetId = _authTargetId, ScanProfileId = Guid.Empty }, null);
        Assert.False(result.Succeeded);
        Assert.Contains("profile", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_UnauthorizedTarget_Fails()
    {
        var result = await CreateService().CreateAsync(new CreateScanRequest { TargetId = _unauthTargetId, ScanProfileId = _profileId }, "admin");
        Assert.False(result.Succeeded);
        Assert.Contains("eligible", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_ArchivedTarget_Fails()
    {
        var result = await CreateService().CreateAsync(new CreateScanRequest { TargetId = _archivedTargetId, ScanProfileId = _profileId }, "admin");
        Assert.False(result.Succeeded);
        Assert.Contains("Archived", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_DisabledTarget_Fails()
    {
        var result = await CreateService().CreateAsync(new CreateScanRequest { TargetId = _disabledTargetId, ScanProfileId = _profileId }, "admin");
        Assert.False(result.Succeeded);
        Assert.Contains("Disabled", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Cancel_Queued_Succeeds()
    {
        var scans = new InMemoryScanRepository();
        var svc = CreateService(scans);
        var created = await svc.CreateAsync(new CreateScanRequest { TargetId = _authTargetId, ScanProfileId = _profileId }, "admin");
        var result = await svc.CancelAsync(created.Scan!.Id);
        Assert.True(result.Succeeded);
        Assert.Equal(ScanStatus.Cancelled, result.Scan!.Status);
        Assert.Equal(0, await svc.GetActiveCountAsync());
    }

    [Fact]
    public async Task Cancel_Completed_Fails()
    {
        var scans = new InMemoryScanRepository();
        var scan = new Scan { TargetId = _authTargetId, ScanProfileId = _profileId, Name = "done", Status = ScanStatus.Completed };
        await scans.AddAsync(scan);
        var result = await CreateService(scans).CancelAsync(scan.Id);
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Search_FilterByStatus()
    {
        var scans = new InMemoryScanRepository();
        var svc = CreateService(scans);
        await svc.CreateAsync(new CreateScanRequest { TargetId = _authTargetId, ScanProfileId = _profileId, Name = "A" }, "admin");
        var list = await svc.SearchAsync(null, ScanStatus.Queued, null, null);
        Assert.Single(list);
    }

    [Fact]
    public async Task GetEligibleTargets_OnlyAuthorizedActive()
    {
        var list = await CreateService().GetEligibleTargetsAsync();
        Assert.Single(list);
        Assert.Equal(_authTargetId, list[0].Id);
    }

    private sealed class InMemoryScanRepository : IScanRepository
    {
        private readonly List<Scan> _items = new();
        public Task<IReadOnlyList<Scan>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Scan>>(_items.ToList());
        public Task<IReadOnlyList<Scan>> SearchAsync(string? searchText, ScanStatus? statusFilter, Guid? targetId, Guid? profileId, CancellationToken cancellationToken = default)
        {
            IEnumerable<Scan> q = _items;
            if (statusFilter.HasValue) q = q.Where(s => s.Status == statusFilter.Value);
            if (targetId.HasValue) q = q.Where(s => s.TargetId == targetId.Value);
            if (profileId.HasValue) q = q.Where(s => s.ScanProfileId == profileId.Value);
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                var t = searchText.Trim().ToLowerInvariant();
                q = q.Where(s => s.Name.ToLower().Contains(t));
            }
            return Task.FromResult<IReadOnlyList<Scan>>(q.ToList());
        }
        public Task<Scan?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(_items.FirstOrDefault(s => s.Id == id));
        public Task<int> CountAsync(CancellationToken cancellationToken = default) => Task.FromResult(_items.Count);
        public Task<int> CountActiveAsync(CancellationToken cancellationToken = default) => Task.FromResult(_items.Count(s => s.Status is ScanStatus.Queued or ScanStatus.Running));
        public Task<Scan> AddAsync(Scan scan, CancellationToken cancellationToken = default) { if (scan.Id == Guid.Empty) scan.Id = Guid.NewGuid(); _items.Add(scan); return Task.FromResult(scan); }
        public Task UpdateAsync(Scan scan, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class InMemoryScanProfileRepository : IScanProfileRepository
    {
        private readonly ScanProfile _profile;
        public InMemoryScanProfileRepository(Guid id) { _profile = new ScanProfile { Id = id, Name = "Discovery", IsEnabled = true }; }
        public Task<IReadOnlyList<ScanProfile>> GetEnabledAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ScanProfile>>(new[] { _profile });
        public Task<ScanProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(id == _profile.Id ? _profile : null);
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
}
