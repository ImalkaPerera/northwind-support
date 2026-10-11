using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SupportDesk.Application.Abstractions;

namespace SupportDesk.Presentation.Security;

/// <summary>
/// Issues the signed token an agent sends back with every request. It carries who the agent is
/// (id, name, email) and when it expires, and nothing secret.
/// </summary>
public sealed class JwtTokenIssuer(JwtSettings settings, IClock clock) : ITokenIssuer
{
    private readonly JsonWebTokenHandler _handler = new();

    public IssuedToken Issue(int agentId, string fullName, string email)
    {
        var issuedAt = clock.UtcNow;
        var expiresAt = issuedAt + settings.Lifetime;

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = settings.Issuer,
            Audience = settings.Audience,
            IssuedAt = issuedAt,
            NotBefore = issuedAt,
            Expires = expiresAt,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, agentId.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                new Claim(JwtRegisteredClaimNames.Name, fullName),
                new Claim(JwtRegisteredClaimNames.Email, email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
            ]),
            SigningCredentials = new SigningCredentials(settings.CreateKey(), SecurityAlgorithms.HmacSha256)
        };

        return new IssuedToken(_handler.CreateToken(descriptor), expiresAt);
    }
}
