using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Presentation.Security;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Presentation;

/// <summary>
/// Starts the whole application in memory, on SQLite, and sends real HTTP requests through the
/// real authentication, so what is checked is the central rule and not a single handler.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public const string SigningKey = "api-test-signing-key-0123456789-abcdef";

    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _connection.Open();

        // "Testing" is not Development, so the app does not try to migrate a SQL Server database.
        builder.UseEnvironment("Testing");
        builder.UseSetting("Jwt:SigningKey", SigningKey);
        builder.UseSetting("ConnectionStrings:Default", "Server=unused;Database=unused");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<SupportDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<SupportDbContext>>();
            services.AddDbContext<SupportDbContext>(options => options.UseSqlite(_connection));
        });
    }

    public async Task SeedAsync()
    {
        using var scope = Services.CreateScope();

        await scope.ServiceProvider.GetRequiredService<SupportDbContext>().Database.EnsureCreatedAsync();
        await scope.ServiceProvider.GetRequiredService<SupportDbSeeder>().SeedAsync();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _connection.Dispose();
        }
    }
}

public sealed class ApiAuthenticationTests : IClassFixture<ApiFactory>, IAsyncLifetime
{
    private const string AlexEmail = "alex.turner@northwind-support.example";
    private const string SaraEmail = "sara.lindqvist@northwind-support.example";
    private const string YukiEmail = "yuki.tanaka@northwind-support.example";

    private readonly ApiFactory _factory;

    public ApiAuthenticationTests(ApiFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.SeedAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<(HttpResponseMessage Response, JsonElement Body)> LoginAsync(string email, string password)
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { email, password });
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();

        return (response, body);
    }

    private async Task<HttpClient> SignedInClientAsync(string email)
    {
        var (_, body) = await LoginAsync(email, SupportDbSeeder.DevelopmentPassword);
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.GetProperty("token").GetString());

        return client;
    }

    [Fact]
    public async Task AProtectedEndpoint_Is401WithoutAToken()
    {
        var response = await _factory.CreateClient().GetAsync("/api/tickets");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/tickets")]
    [InlineData("/api/tickets/1")]
    [InlineData("/api/agents")]
    [InlineData("/api/customers")]
    [InlineData("/api/categories")]
    [InlineData("/api/auth/me")]
    public async Task EveryExistingEndpoint_IsClosedWithoutAToken(string path)
    {
        var response = await _factory.CreateClient().GetAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AProtectedEndpoint_Is200WithAValidToken()
    {
        var client = await SignedInClientAsync(AlexEmail);

        var response = await client.GetAsync("/api/tickets");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ATokenSignedWithAnotherKeyIsRejected()
    {
        var forged = new JwtTokenIssuer(
            new JwtSettings("some-other-signing-key-0123456789-abcdef", "SupportDesk", "SupportDesk.Web", TimeSpan.FromHours(1)),
            new FixedClockFromNow()).Issue(1, "Alex Turner", AlexEmail);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", forged.Token);

        var response = await client.GetAsync("/api/tickets");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AnExpiredTokenIsRejected()
    {
        var longAgo = new FixedClock(DateTime.UtcNow.AddHours(-3));
        var expired = new JwtTokenIssuer(
            new JwtSettings(ApiFactory.SigningKey, "SupportDesk", "SupportDesk.Web", TimeSpan.FromMinutes(60)),
            longAgo).Issue(1, "Alex Turner", AlexEmail);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", expired.Token);

        var response = await client.GetAsync("/api/tickets");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithTheRightPassword_ReturnsATokenItsExpiryAndTheUser_AndNoHash()
    {
        var (response, body) = await LoginAsync(AlexEmail, SupportDbSeeder.DevelopmentPassword);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("token").GetString()));
        Assert.True(body.GetProperty("expiresAtUtc").GetDateTime() > DateTime.UtcNow);
        Assert.Equal("Alex Turner", body.GetProperty("user").GetProperty("fullName").GetString());
        Assert.DoesNotContain("passwordHash", body.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AQAAAA", body.ToString());
    }

    [Fact]
    public async Task Login_WithAWrongPasswordOrAnUnknownEmail_GivesTheSame401AndMessage()
    {
        var (wrongPassword, wrongBody) = await LoginAsync(AlexEmail, "not-the-password");
        var (unknownEmail, unknownBody) = await LoginAsync("nobody@example.test", SupportDbSeeder.DevelopmentPassword);

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);
        Assert.Equal(
            wrongBody.GetProperty("detail").GetString(),
            unknownBody.GetProperty("detail").GetString());
        Assert.False(wrongBody.TryGetProperty("token", out _));
    }

    [Fact]
    public async Task Login_AsAnInactiveAgent_IsRejectedWithTheSameMessage()
    {
        var (inactive, inactiveBody) = await LoginAsync(YukiEmail, SupportDbSeeder.DevelopmentPassword);
        var (wrong, wrongBody) = await LoginAsync(AlexEmail, "not-the-password");

        Assert.Equal(HttpStatusCode.Unauthorized, inactive.StatusCode);
        Assert.Equal(
            wrongBody.GetProperty("detail").GetString(),
            inactiveBody.GetProperty("detail").GetString());
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
    }

    [Fact]
    public async Task Login_IsOpenWithoutAToken_AndRejectsAnEmptyBodyWith400()
    {
        var response = await _factory.CreateClient()
            .PostAsJsonAsync("/api/auth/login", new { email = "", password = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Me_ReturnsTheSignedInAgent()
    {
        var client = await SignedInClientAsync(SaraEmail);

        var response = await client.GetAsync("/api/auth/me");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Sara Lindqvist", body.GetProperty("fullName").GetString());
    }

    [Fact]
    public async Task Escalation_RecordsTheSignedInAgent_NotAnythingInTheBody()
    {
        var client = await SignedInClientAsync(SaraEmail);

        // The body tries to say someone else did it. It is ignored.
        var response = await client.PostAsJsonAsync(
            "/api/tickets/3/escalate",
            new { reason = "Customer called twice, need a faster answer", escalatedBy = "Someone Else" });

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Sara Lindqvist", body.GetProperty("escalation").GetProperty("escalatedBy").GetString());
    }

    [Fact]
    public async Task Escalation_WithoutAToken_IsRejected()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync(
            "/api/tickets/3/escalate",
            new { reason = "Customer called twice, need a faster answer" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>A clock that reads the real time, for tokens that should be valid right now.</summary>
    private sealed class FixedClockFromNow : SupportDesk.Application.Abstractions.IClock
    {
        public DateTime UtcNow => DateTime.UtcNow;
    }
}
