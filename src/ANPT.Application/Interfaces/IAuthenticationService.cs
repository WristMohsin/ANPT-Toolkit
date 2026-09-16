using ANPT.Application.Models;

namespace ANPT.Application.Interfaces;

public interface IAuthenticationService
{
    /// <summary>
    /// Attempts to authenticate with username and password.
    /// Returns a generic failure message on any credential problem to avoid user enumeration.
    /// </summary>
    Task<AuthenticationResult> LoginAsync(string username, string password, CancellationToken cancellationToken = default);
}
