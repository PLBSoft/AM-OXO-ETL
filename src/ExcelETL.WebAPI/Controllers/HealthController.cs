using ExcelETL.Infrastructure.Persistence;
using ExcelETL.WebAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExcelETL.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController(
    IDbContextFactory<ExcelEtlDbContext> dbContextFactory,
    ApiBuildInfo buildInfo)
    : ControllerBase
{
    // Bounds how long a down/unreachable database can make this endpoint hang -- a health check
    // is meant to answer quickly, not wait out the database's own (much longer) connection timeout.
    private static readonly TimeSpan DatabaseCheckTimeout = TimeSpan.FromSeconds(5);

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var databaseHealthy = await CheckDatabaseAsync(cancellationToken);

        return Ok(new
        {
            status = databaseHealthy ? "Healthy" : "Degraded",
            version = buildInfo.Version,
            database = databaseHealthy ? "Healthy" : "Unhealthy"
        });
    }

    [HttpGet("ping")]
    public IActionResult Ping() => Ok(new { status = "Pong", timestampUtc = DateTimeOffset.UtcNow });

    private async Task<bool> CheckDatabaseAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(DatabaseCheckTimeout);

            await using var dbContext = await dbContextFactory.CreateDbContextAsync(timeoutCts.Token);
            return await dbContext.Database.CanConnectAsync(timeoutCts.Token);
        }
        catch
        {
            return false;
        }
    }
}
