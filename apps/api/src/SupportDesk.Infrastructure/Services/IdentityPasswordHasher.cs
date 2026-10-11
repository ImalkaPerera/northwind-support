using Microsoft.AspNetCore.Identity;
using SupportDesk.Application.Abstractions;

namespace SupportDesk.Infrastructure.Services;

/// <summary>
/// Password hashing with ASP.NET Core Identity's <see cref="PasswordHasher{TUser}"/>: PBKDF2
/// with HMAC-SHA512, a random salt per password, and an iteration count stored inside the hash
/// so it can be raised later. Nothing here is home-made.
/// </summary>
public sealed class IdentityPasswordHasher : IPasswordHasher
{
    private static readonly object NoUser = new();

    private readonly PasswordHasher<object> _hasher = new();

    public string Hash(string password) => _hasher.HashPassword(NoUser, password);

    public bool Verify(string passwordHash, string password)
    {
        var result = _hasher.VerifyHashedPassword(NoUser, passwordHash, password);

        return result is PasswordVerificationResult.Success
            or PasswordVerificationResult.SuccessRehashNeeded;
    }
}
