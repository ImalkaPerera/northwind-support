namespace SupportDesk.Application.Abstractions;

/// <summary>
/// Hashes and checks passwords. The implementation uses a vetted algorithm (salted, iterated
/// PBKDF2); nothing in the application ever handles a password without going through this.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>A salted hash of the password. The same password gives a different hash each time.</summary>
    string Hash(string password);

    /// <summary>True when the password matches the hash.</summary>
    bool Verify(string passwordHash, string password);
}

/// <summary>A signed access token and when it stops being valid.</summary>
public sealed record IssuedToken(string Token, DateTime ExpiresAtUtc);

/// <summary>Creates the signed token a signed-in agent sends with every request.</summary>
public interface ITokenIssuer
{
    IssuedToken Issue(int agentId, string fullName, string email);
}

/// <summary>
/// The agent making the current request, as the token says. The application never trusts a
/// name or id from a request body for "who did this".
/// </summary>
public interface ICurrentUser
{
    int? AgentId { get; }

    string? FullName { get; }
}
