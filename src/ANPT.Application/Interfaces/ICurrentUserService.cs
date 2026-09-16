using ANPT.Application.Models;
using ANPT.Domain.Enums;

namespace ANPT.Application.Interfaces;

/// <summary>
/// Application-wide authenticated session. Singleton lifetime.
/// </summary>
public interface ICurrentUserService
{
    bool IsAuthenticated { get; }
    Guid? UserId { get; }
    string? Username { get; }
    string? DisplayName { get; }
    UserRole? Role { get; }
    AuthenticatedUser? User { get; }

    void SetUser(AuthenticatedUser user);
    void Clear();
}
