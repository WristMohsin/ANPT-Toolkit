using ANPT.Application.Interfaces;
using ANPT.Application.Models;
using Microsoft.Extensions.Logging;

namespace ANPT.Application.Services;

public class AuthenticationService : IAuthenticationService
{
    private const string GenericFailure = "Invalid username or password.";
    private const string InactiveFailure = "This account is inactive. Contact an administrator.";

    private readonly IUserRepository _users;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<AuthenticationService> _logger;

    public AuthenticationService(
        IUserRepository users,
        IPasswordHasher passwordHasher,
        ILogger<AuthenticationService> logger)
    {
        _users = users;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    public async Task<AuthenticationResult> LoginAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        username = (username ?? string.Empty).Trim();
        password ??= string.Empty;

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
        {
            _logger.LogWarning("Login attempt with empty credentials");
            return AuthenticationResult.Failure(GenericFailure);
        }

        if (username.Length > 100 || password.Length > 256)
        {
            _logger.LogWarning("Login attempt with oversized credentials for username length {Length}", username.Length);
            return AuthenticationResult.Failure(GenericFailure);
        }

        try
        {
            var user = await _users.FindByUsernameAsync(username, cancellationToken);

            if (user is null)
            {
                _logger.LogWarning("Login failed: user not found for username {Username}", username);
                return AuthenticationResult.Failure(GenericFailure);
            }

            if (!user.IsActive)
            {
                _logger.LogWarning("Login failed: inactive account {Username}", username);
                return AuthenticationResult.Failure(InactiveFailure);
            }

            if (!_passwordHasher.VerifyPassword(password, user.PasswordHash))
            {
                _logger.LogWarning("Login failed: invalid password for username {Username}", username);
                return AuthenticationResult.Failure(GenericFailure);
            }

            user.LastLoginAt = DateTime.UtcNow;
            user.UpdatedAt = DateTime.UtcNow;
            await _users.UpdateAsync(user, cancellationToken);

            var authUser = new AuthenticatedUser
            {
                UserId = user.Id,
                Username = user.Username,
                DisplayName = string.IsNullOrWhiteSpace(user.DisplayName) ? user.Username : user.DisplayName,
                Role = user.Role
            };

            _logger.LogInformation("Login succeeded for user {Username} with role {Role}", authUser.Username, authUser.Role);
            return AuthenticationResult.Success(authUser);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during login for username {Username}", username);
            return AuthenticationResult.Failure("Authentication is temporarily unavailable. Please try again.");
        }
    }
}
