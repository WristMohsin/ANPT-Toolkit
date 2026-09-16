using ANPT.Domain.Enums;

namespace ANPT.Application.Models;

/// <summary>
/// Immutable snapshot of an authenticated user for session use.
/// Does not contain password or hash material.
/// </summary>
public sealed class AuthenticatedUser
{
    public Guid UserId { get; init; }
    public string Username { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public UserRole Role { get; init; }

    public bool IsAdmin => Role == UserRole.Admin;
    public bool IsAnalyst => Role == UserRole.Analyst || Role == UserRole.Admin;
    public bool IsViewer => true;
}
