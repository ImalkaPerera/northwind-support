using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Auth.Queries.GetCurrentUser;

/// <summary>
/// Who the token belongs to, checked against the database: an agent who has been deactivated
/// since the token was issued is no longer signed in.
/// </summary>
public sealed class GetCurrentUserQueryHandler(ICurrentUser currentUser, IAgentRepository agents)
{
    /// <exception cref="UnauthorizedException">There is no valid, active agent behind the token.</exception>
    public async Task<CurrentUserDto> HandleAsync(CancellationToken ct)
    {
        if (currentUser.AgentId is not { } id)
        {
            throw new UnauthorizedException("You are not signed in.");
        }

        var agent = await agents.GetByIdAsync(id, ct);

        if (agent is null || !agent.IsActive)
        {
            throw new UnauthorizedException("You are not signed in.");
        }

        return new CurrentUserDto(agent.Id, agent.FullName, agent.Email);
    }
}
