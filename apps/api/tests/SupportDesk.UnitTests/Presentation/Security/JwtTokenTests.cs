using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SupportDesk.Presentation.Security;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Presentation.Security;

public class JwtTokenTests
{
    private const string Key = "unit-test-signing-key-0123456789-abcdef";

    private static JwtSettings Settings(int lifetimeMinutes = 60) =>
        new(Key, "SupportDesk", "SupportDesk.Web", TimeSpan.FromMinutes(lifetimeMinutes));

    private static IConfiguration Configuration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static Dictionary<string, string?> ValidSettings() => new()
    {
        ["Jwt:SigningKey"] = Key,
        ["Jwt:Issuer"] = "SupportDesk",
        ["Jwt:Audience"] = "SupportDesk.Web",
        ["Jwt:LifetimeMinutes"] = "60"
    };

    [Fact]
    public void TheTokenCarriesWhoTheAgentIsAndWhenItExpires()
    {
        var clock = new FixedClock();
        var issued = new JwtTokenIssuer(Settings(60), clock).Issue(4, "Sara Lindqvist", "sara@example.test");

        var token = new JsonWebToken(issued.Token);

        Assert.Equal("4", token.Subject);
        Assert.Equal("Sara Lindqvist", token.GetClaim(JwtRegisteredClaimNames.Name).Value);
        Assert.Equal("sara@example.test", token.GetClaim(JwtRegisteredClaimNames.Email).Value);
        Assert.Equal(clock.UtcNow.AddMinutes(60), issued.ExpiresAtUtc);
        Assert.Equal(issued.ExpiresAtUtc, token.ValidTo);
    }

    [Fact]
    public async Task TheTokenIsSignedWithTheConfiguredKey()
    {
        var issued = new JwtTokenIssuer(Settings(), new FixedClock()).Issue(1, "Alex Turner", "alex@example.test");

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(
            issued.Token,
            new TokenValidationParameters
            {
                ValidIssuer = "SupportDesk",
                ValidAudience = "SupportDesk.Web",
                IssuerSigningKey = Settings().CreateKey(),
                ValidateLifetime = false
            });

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ATokenSignedWithAnotherKeyIsRejected()
    {
        var issued = new JwtTokenIssuer(Settings(), new FixedClock()).Issue(1, "Alex Turner", "alex@example.test");

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(
            issued.Token,
            new TokenValidationParameters
            {
                ValidIssuer = "SupportDesk",
                ValidAudience = "SupportDesk.Web",
                IssuerSigningKey = new SymmetricSecurityKey("a-different-key-0123456789-0123456789"u8.ToArray()),
                ValidateLifetime = false
            });

        Assert.False(result.IsValid);
    }

    [Fact]
    public void TheSettingsAreReadFromConfiguration()
    {
        var settings = JwtSettings.FromConfiguration(Configuration(ValidSettings()));

        Assert.Equal(TimeSpan.FromMinutes(60), settings.Lifetime);
        Assert.Equal("SupportDesk", settings.Issuer);
    }

    [Fact]
    public void AMissingSigningKeyStopsStartupAndDoesNotRevealAnyValue()
    {
        var values = ValidSettings();
        values.Remove("Jwt:SigningKey");

        var exception = Assert.Throws<InvalidOperationException>(
            () => JwtSettings.FromConfiguration(Configuration(values)));

        Assert.Contains("Jwt__SigningKey", exception.Message);
    }

    [Fact]
    public void AKeyThatIsTooShortIsRejected()
    {
        var values = ValidSettings();
        values["Jwt:SigningKey"] = "too-short";

        Assert.Throws<InvalidOperationException>(() => JwtSettings.FromConfiguration(Configuration(values)));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("an hour")]
    public void ALifetimeThatIsNotAPositiveNumberIsRejected(string minutes)
    {
        var values = ValidSettings();
        values["Jwt:LifetimeMinutes"] = minutes;

        Assert.Throws<InvalidOperationException>(() => JwtSettings.FromConfiguration(Configuration(values)));
    }
}
