using Microsoft.Extensions.Logging;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Auth.Commands.Login;

/// <summary>
/// Signs an agent in with their email and password and issues a token.
/// </summary>
/// <remarks>
/// Every way of failing - unknown email, wrong password, inactive agent, no password set -
/// gives the same exception with the same message, so a caller cannot tell which it was. When
/// there is no password to check against, the supplied one is hashed and thrown away, so the
/// response takes about as long and the timing does not give the email away either.
/// </remarks>
public sealed class LoginCommandHandler(
    IAgentRepository agents,
    IPasswordHasher passwordHasher,
    ITokenIssuer tokenIssuer,
    ILogger<LoginCommandHandler> logger)
{
    public const string FailureMessage = "Invalid email or password.";

    /// <exception cref="UnauthorizedException">The email or the password is wrong.</exception>
    public async Task<LoginResponse> HandleAsync(LoginRequest request, CancellationToken ct)
    {
        var agent = await agents.GetByEmailAsync(request.Email.Trim(), ct);

        if (agent?.PasswordHash is not { } storedHash)
        {
            _ = passwordHasher.Hash(request.Password);

            throw Rejected();
        }

        var passwordMatches = passwordHasher.Verify(storedHash, request.Password);

        if (!passwordMatches || !agent.IsActive)
        {
            throw Rejected();
        }

        var issued = tokenIssuer.Issue(agent.Id, agent.FullName, agent.Email);

        logger.LogInformation("Agent {AgentId} signed in.", agent.Id);

        return new LoginResponse(
            issued.Token,
            issued.ExpiresAtUtc,
            new CurrentUserDto(agent.Id, agent.FullName, agent.Email));
    }

    private UnauthorizedException Rejected()
    {
        logger.LogInformation("A sign-in attempt was rejected.");

        return new UnauthorizedException(FailureMessage);
    }
}
