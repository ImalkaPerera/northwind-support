using Microsoft.Extensions.Diagnostics.HealthChecks;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Presentation.Health;

/// <summary>
/// Says whether the database can be reached. Nothing is read or written, so it is cheap enough
/// for Docker or a load balancer to call every few seconds.
/// </summary>
public sealed class DatabaseHealthCheck(SupportDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await db.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy("The database is reachable.")
                : HealthCheckResult.Unhealthy("The database cannot be reached.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The detail stays in the log. The response only says that it failed.
            return HealthCheckResult.Unhealthy("The database cannot be reached.", exception);
        }
    }
}
