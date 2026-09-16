using ANPT.Application.Interfaces;
using ANPT.Application.Models;
using ANPT.Domain.Enums;

namespace ANPT.Application.Services;

public class CurrentUserService : ICurrentUserService
{
    private AuthenticatedUser? _user;

    public bool IsAuthenticated => _user is not null;
    public Guid? UserId => _user?.UserId;
    public string? Username => _user?.Username;
    public string? DisplayName => _user?.DisplayName;
    public UserRole? Role => _user?.Role;
    public AuthenticatedUser? User => _user;

    public void SetUser(AuthenticatedUser user)
    {
        _user = user ?? throw new ArgumentNullException(nameof(user));
    }

    public void Clear()
    {
        _user = null;
    }
}
