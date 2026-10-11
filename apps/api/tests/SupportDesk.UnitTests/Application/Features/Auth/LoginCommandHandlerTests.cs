using Microsoft.Extensions.Logging.Abstractions;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Auth.Commands.Login;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Repositories;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Auth;

public class LoginCommandHandlerTests
{
    private const string Email = "sara.lindqvist@northwind-support.example";
    private const string StoredHash = "stored-hash";
    private const string Password = "LocalDev-Password-1";

    private readonly Mock<IAgentRepository> _agents = new();
    private readonly Mock<IPasswordHasher> _hasher = new();
    private readonly Mock<ITokenIssuer> _tokens = new();
    private readonly LoginCommandHandler _handler;

    public LoginCommandHandlerTests()
    {
        _hasher.Setup(h => h.Hash(It.IsAny<string>())).Returns("decoy-hash");
        _hasher.Setup(h => h.Verify(StoredHash, Password)).Returns(true);
        _hasher.Setup(h => h.Verify(It.IsAny<string>(), It.Is<string>(p => p != Password))).Returns(false);

        _tokens.Setup(t => t.Issue(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(new IssuedToken("signed-token", FixedClock.DefaultNow.AddMinutes(60)));

        _handler = new LoginCommandHandler(
            _agents.Object, _hasher.Object, _tokens.Object, NullLogger<LoginCommandHandler>.Instance);
    }

    private static Agent AgentWithPassword(bool active = true, string? hash = StoredHash)
    {
        var agent = new Agent("Sara Lindqvist", Email, 6, FixedClock.DefaultNow);

        if (hash is not null)
        {
            agent.SetPasswordHash(hash);
        }

        if (!active)
        {
            agent.Deactivate();
        }

        return agent;
    }

    private void AgentExists(Agent agent) =>
        _agents.Setup(a => a.GetByEmailAsync(Email, It.IsAny<CancellationToken>())).ReturnsAsync(agent);

    [Fact]
    public async Task TheRightEmailAndPassword_SignIn()
    {
        AgentExists(AgentWithPassword());

        var response = await _handler.HandleAsync(new LoginRequest(Email, Password), CancellationToken.None);

        Assert.Equal("signed-token", response.Token);
        Assert.Equal(FixedClock.DefaultNow.AddMinutes(60), response.ExpiresAtUtc);
        Assert.Equal("Sara Lindqvist", response.User.FullName);
        Assert.Equal(Email, response.User.Email);
    }

    [Fact]
    public async Task AWrongPassword_IsRejectedAndNoTokenIsIssued()
    {
        AgentExists(AgentWithPassword());

        var exception = await Assert.ThrowsAsync<UnauthorizedException>(
            () => _handler.HandleAsync(new LoginRequest(Email, "not-the-password"), CancellationToken.None));

        Assert.Equal(LoginCommandHandler.FailureMessage, exception.Message);
        _tokens.Verify(t => t.Issue(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never());
    }

    [Fact]
    public async Task AnUnknownEmail_GivesTheSameMessageAsAWrongPassword()
    {
        _agents.Setup(a => a.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Agent?)null);
        AgentExists(AgentWithPassword());

        var wrongPassword = await Assert.ThrowsAsync<UnauthorizedException>(
            () => _handler.HandleAsync(new LoginRequest(Email, "not-the-password"), CancellationToken.None));

        var unknownEmail = await Assert.ThrowsAsync<UnauthorizedException>(
            () => _handler.HandleAsync(new LoginRequest("nobody@example.com", Password), CancellationToken.None));

        Assert.Equal(wrongPassword.Message, unknownEmail.Message);
    }

    [Fact]
    public async Task AnUnknownEmail_StillDoesTheHashingWork_SoTheResponseTimeDoesNotGiveItAway()
    {
        _agents.Setup(a => a.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Agent?)null);

        await Assert.ThrowsAsync<UnauthorizedException>(
            () => _handler.HandleAsync(new LoginRequest("nobody@example.com", Password), CancellationToken.None));

        _hasher.Verify(h => h.Hash(Password), Times.Once());
        _tokens.Verify(t => t.Issue(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never());
    }

    [Fact]
    public async Task AnInactiveAgent_CannotSignInEvenWithTheRightPassword()
    {
        AgentExists(AgentWithPassword(active: false));

        var exception = await Assert.ThrowsAsync<UnauthorizedException>(
            () => _handler.HandleAsync(new LoginRequest(Email, Password), CancellationToken.None));

        Assert.Equal(LoginCommandHandler.FailureMessage, exception.Message);
        _tokens.Verify(t => t.Issue(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never());
    }

    [Fact]
    public async Task AnAgentWithNoPassword_CannotSignIn()
    {
        AgentExists(AgentWithPassword(hash: null));

        await Assert.ThrowsAsync<UnauthorizedException>(
            () => _handler.HandleAsync(new LoginRequest(Email, Password), CancellationToken.None));
    }

    [Fact]
    public async Task TheEmailIsTrimmedBeforeTheLookup()
    {
        AgentExists(AgentWithPassword());

        var response = await _handler.HandleAsync(new LoginRequest($"  {Email}  ", Password), CancellationToken.None);

        Assert.Equal(Email, response.User.Email);
    }
}
