namespace ANPT.Application.Models;

public sealed class AuthenticationResult
{
    public bool Succeeded { get; init; }
    public string? ErrorMessage { get; init; }
    public AuthenticatedUser? User { get; init; }

    public static AuthenticationResult Success(AuthenticatedUser user) =>
        new() { Succeeded = true, User = user };

    public static AuthenticationResult Failure(string message) =>
        new() { Succeeded = false, ErrorMessage = message };
}
