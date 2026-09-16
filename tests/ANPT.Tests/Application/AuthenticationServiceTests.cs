using ANPT.Application.Interfaces;
using ANPT.Application.Services;
using ANPT.Domain.Entities;
using ANPT.Domain.Enums;
using ANPT.Infrastructure.Security;
using Microsoft.Extensions.Logging.Abstractions;

namespace ANPT.Tests.Application;

public class AuthenticationServiceTests
{
    private readonly Pbkdf2PasswordHasher _hasher = new();

    private AuthenticationService CreateService(IUserRepository repo) =>
        new(repo, _hasher, NullLogger<AuthenticationService>.Instance);

    [Fact]
    public async Task Login_ValidCredentials_Succeeds()
    {
        var password = "ValidPass1!";
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "tester",
            DisplayName = "Test User",
            PasswordHash = _hasher.HashPassword(password),
            Role = UserRole.Analyst,
            IsActive = true
        };
        var repo = new InMemoryUserRepository(user);
        var svc = CreateService(repo);

        var result = await svc.LoginAsync("tester", password);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.User);
        Assert.Equal("tester", result.User.Username);
        Assert.Equal(UserRole.Analyst, result.User.Role);
    }

    [Fact]
    public async Task Login_InvalidPassword_Fails()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "tester",
            DisplayName = "Test",
            PasswordHash = _hasher.HashPassword("RightPass!"),
            Role = UserRole.Viewer,
            IsActive = true
        };
        var svc = CreateService(new InMemoryUserRepository(user));

        var result = await svc.LoginAsync("tester", "WrongPass!");

        Assert.False(result.Succeeded);
        Assert.Equal("Invalid username or password.", result.ErrorMessage);
    }

    [Fact]
    public async Task Login_UnknownUser_FailsGenerically()
    {
        var svc = CreateService(new InMemoryUserRepository());
        var result = await svc.LoginAsync("nobody", "whatever");
        Assert.False(result.Succeeded);
        Assert.Equal("Invalid username or password.", result.ErrorMessage);
    }

    [Fact]
    public async Task Login_InactiveUser_Fails()
    {
        var password = "Pass1!";
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "inactive",
            DisplayName = "Inactive",
            PasswordHash = _hasher.HashPassword(password),
            Role = UserRole.Viewer,
            IsActive = false
        };
        var svc = CreateService(new InMemoryUserRepository(user));

        var result = await svc.LoginAsync("inactive", password);

        Assert.False(result.Succeeded);
        Assert.Contains("inactive", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_EmptyUsername_Fails()
    {
        var svc = CreateService(new InMemoryUserRepository());
        var result = await svc.LoginAsync("  ", "password");
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Login_EmptyPassword_Fails()
    {
        var svc = CreateService(new InMemoryUserRepository());
        var result = await svc.LoginAsync("user", "");
        Assert.False(result.Succeeded);
    }

    private sealed class InMemoryUserRepository : IUserRepository
    {
        private readonly List<User> _users;

        public InMemoryUserRepository(params User[] users)
        {
            _users = users.ToList();
        }

        public Task<User?> FindByUsernameAsync(string username, CancellationToken cancellationToken = default)
        {
            var u = _users.FirstOrDefault(x => x.Username.Equals(username.Trim(), StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(u);
        }

        public Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_users.FirstOrDefault(x => x.Id == id));

        public Task<bool> ExistsAsync(string username, CancellationToken cancellationToken = default) =>
            Task.FromResult(_users.Any(x => x.Username.Equals(username.Trim(), StringComparison.OrdinalIgnoreCase)));

        public Task<User> CreateAsync(User user, CancellationToken cancellationToken = default)
        {
            _users.Add(user);
            return Task.FromResult(user);
        }

        public Task UpdateAsync(User user, CancellationToken cancellationToken = default)
        {
            var idx = _users.FindIndex(x => x.Id == user.Id);
            if (idx >= 0) _users[idx] = user;
            return Task.CompletedTask;
        }
    }
}
