using System.Globalization;
using Microsoft.IdentityModel.JsonWebTokens;
using SupportDesk.Application.Abstractions;

namespace SupportDesk.Presentation.Security;

/// <summary>
/// The agent behind the token on the current request. The values come from claims the server
/// signed, so they cannot be changed by the caller.
/// </summary>
public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public int? AgentId =>
        int.TryParse(
            accessor.HttpContext?.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var id)
            ? id
            : null;

    public string? FullName => accessor.HttpContext?.User.FindFirst(JwtRegisteredClaimNames.Name)?.Value;
}
