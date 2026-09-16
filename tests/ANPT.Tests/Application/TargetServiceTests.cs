using ANPT.Application.Interfaces;
using ANPT.Application.Services;
using ANPT.Domain.Entities;
using ANPT.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;

namespace ANPT.Tests.Application;

public class TargetServiceTests
{
    private TargetService CreateService(ITargetRepository repo) =>
        new(repo, NullLogger<TargetService>.Instance);

    [Fact]
    public async Task Create_ValidTarget_Succeeds()
    {
        var repo = new InMemoryTargetRepository();
        var svc = CreateService(repo);

        var result = await svc.CreateAsync(new CreateTargetRequest
        {
            Name = "Lab Gateway",
            Address = "192.168.1.1",
            TargetType = "Host",
            AuthorizationConfirmed = true
        }, "admin");

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Target);
        Assert.Equal("Lab Gateway", result.Target.Name);
        Assert.Equal("192.168.1.1", result.Target.Address);
        Assert.True(result.Target.AuthorizationConfirmed);
        Assert.Equal(TargetStatus.Active, result.Target.Status);
        Assert.Equal("admin", result.Target.CreatedBy);
    }

    [Fact]
    public async Task Create_MissingName_Fails()
    {
        var svc = CreateService(new InMemoryTargetRepository());
        var result = await svc.CreateAsync(new CreateTargetRequest
        {
            Name = "  ",
            Address = "10.0.0.1"
        }, null);

        Assert.False(result.Succeeded);
        Assert.Contains("name", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_MissingAddress_Fails()
    {
        var svc = CreateService(new InMemoryTargetRepository());
        var result = await svc.CreateAsync(new CreateTargetRequest
        {
            Name = "Something",
            Address = ""
        }, null);

        Assert.False(result.Succeeded);
        Assert.Contains("address", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_Unauthorized_RemainsUnauthorized()
    {
        var repo = new InMemoryTargetRepository();
        var svc = CreateService(repo);

        var result = await svc.CreateAsync(new CreateTargetRequest
        {
            Name = "Unconfirmed",
            Address = "10.0.0.5",
            AuthorizationConfirmed = false
        }, "analyst");

        Assert.True(result.Succeeded);
        Assert.False(result.Target!.AuthorizationConfirmed);
        Assert.False(svc.IsEligibleForScan(result.Target));
    }

    [Fact]
    public async Task Create_Authorized_IsEligibleForScan()
    {
        var repo = new InMemoryTargetRepository();
        var svc = CreateService(repo);

        var result = await svc.CreateAsync(new CreateTargetRequest
        {
            Name = "Confirmed",
            Address = "10.0.0.6",
            AuthorizationConfirmed = true
        }, "admin");

        Assert.True(result.Succeeded);
        Assert.True(svc.IsEligibleForScan(result.Target!));
    }

    [Fact]
    public async Task Update_Valid_Succeeds()
    {
        var repo = new InMemoryTargetRepository();
        var svc = CreateService(repo);
        var created = await svc.CreateAsync(new CreateTargetRequest
        {
            Name = "Old",
            Address = "1.1.1.1",
            AuthorizationConfirmed = false
        }, "admin");

        var result = await svc.UpdateAsync(new UpdateTargetRequest
        {
            Id = created.Target!.Id,
            Name = "New Name",
            Address = "2.2.2.2",
            TargetType = "Network",
            AuthorizationConfirmed = true,
            Status = TargetStatus.Active
        });

        Assert.True(result.Succeeded);
        Assert.Equal("New Name", result.Target!.Name);
        Assert.Equal("2.2.2.2", result.Target.Address);
        Assert.True(result.Target.AuthorizationConfirmed);
        Assert.True(svc.IsEligibleForScan(result.Target));
    }

    [Fact]
    public async Task Update_NotFound_Fails()
    {
        var svc = CreateService(new InMemoryTargetRepository());
        var result = await svc.UpdateAsync(new UpdateTargetRequest
        {
            Id = Guid.NewGuid(),
            Name = "X",
            Address = "1.1.1.1"
        });
        Assert.False(result.Succeeded);
        Assert.Contains("not found", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Archive_SetsStatusArchived_AndNotEligible()
    {
        var repo = new InMemoryTargetRepository();
        var svc = CreateService(repo);
        var created = await svc.CreateAsync(new CreateTargetRequest
        {
            Name = "ToArchive",
            Address = "9.9.9.9",
            AuthorizationConfirmed = true
        }, "admin");

        Assert.True(svc.IsEligibleForScan(created.Target!));

        var archived = await svc.ArchiveAsync(created.Target!.Id);
        Assert.True(archived.Succeeded);
        Assert.Equal(TargetStatus.Archived, archived.Target!.Status);
        Assert.False(svc.IsEligibleForScan(archived.Target));
    }

    [Fact]
    public async Task GetTotalCount_ReflectsPersistedTargets()
    {
        var repo = new InMemoryTargetRepository();
        var svc = CreateService(repo);

        Assert.Equal(0, await svc.GetTotalCountAsync());

        await svc.CreateAsync(new CreateTargetRequest { Name = "A", Address = "1.1.1.1" }, null);
        await svc.CreateAsync(new CreateTargetRequest { Name = "B", Address = "2.2.2.2" }, null);

        Assert.Equal(2, await svc.GetTotalCountAsync());
    }

    [Fact]
    public void IsEligibleForScan_Null_ReturnsFalse()
    {
        var svc = CreateService(new InMemoryTargetRepository());
        Assert.False(svc.IsEligibleForScan(null!));
    }

    private sealed class InMemoryTargetRepository : ITargetRepository
    {
        private readonly List<Target> _items = new();

        public Task<IReadOnlyList<Target>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Target>>(_items.OrderByDescending(t => t.CreatedAt).ToList());

        public Task<IReadOnlyList<Target>> SearchAsync(string? searchText, TargetStatus? statusFilter, CancellationToken cancellationToken = default)
        {
            IEnumerable<Target> q = _items;
            if (statusFilter.HasValue)
                q = q.Where(t => t.Status == statusFilter.Value);
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                var term = searchText.Trim().ToLowerInvariant();
                q = q.Where(t =>
                    t.Name.ToLower().Contains(term) ||
                    t.Address.ToLower().Contains(term));
            }
            return Task.FromResult<IReadOnlyList<Target>>(q.OrderByDescending(t => t.CreatedAt).ToList());
        }

        public Task<Target?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_items.FirstOrDefault(t => t.Id == id));

        public Task<int> CountAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_items.Count);

        public Task<int> CountActiveAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_items.Count(t => t.Status == TargetStatus.Active));

        public Task<Target> AddAsync(Target target, CancellationToken cancellationToken = default)
        {
            _items.Add(target);
            return Task.FromResult(target);
        }

        public Task UpdateAsync(Target target, CancellationToken cancellationToken = default)
        {
            var idx = _items.FindIndex(t => t.Id == target.Id);
            if (idx >= 0)
                _items[idx] = target;
            return Task.CompletedTask;
        }
    }
}
