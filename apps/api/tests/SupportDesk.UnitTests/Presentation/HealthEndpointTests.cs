using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Presentation.Health;

namespace SupportDesk.UnitTests.Presentation;

public sealed class HealthEndpointTests : IClassFixture<ApiFactory>, IAsyncLifetime
{
    private readonly ApiFactory _factory;

    public HealthEndpointTests(ApiFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.SeedAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Health_IsOpenWithoutAToken_AndHealthyWhenTheDatabaseIsReachable()
    {
        var response = await _factory.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task TheCheck_IsUnhealthyWhenTheDatabaseCannotBeReached()
    {
        var options = new DbContextOptionsBuilder<SupportDbContext>()
            .UseSqlite("DataSource=/this-folder-does-not-exist/support.db;Mode=ReadOnly")
            .Options;

        await using var db = new SupportDbContext(options);

        var result = await new DatabaseHealthCheck(db).CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }
}
