using ExcelETL.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ExcelETL.WebAPI.HealthChecks;

// A plain, framework-registered IHealthCheck -- not a controller, so the DbContext access here is
// outside the scope of the "never inject DbContext into a controller/Razor component" rule (the
// original motivation for briefly carving out an exception in CLAUDE.md, since reverted: this
// factoring makes the exception unnecessary for this case, same result reached the proper way
// instead of by relaxing the rule). Registered via AddHealthChecks().AddCheck<DatabaseHealthCheck>
// (Program.cs), bounded by that registration's own timeout -- the framework's HealthCheckService
// cancels a slow check after it, same effective bound the hand-rolled version enforced itself.
public sealed class DatabaseHealthCheck(IDbContextFactory<ExcelEtlDbContext> dbContextFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);
            return canConnect ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(exception: ex);
        }
    }
}
