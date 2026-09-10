using ExcelETL.WebAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ExcelETL.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController(HealthCheckService healthCheckService, ApiBuildInfo buildInfo) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var report = await healthCheckService.CheckHealthAsync(cancellationToken);
        var databaseHealthy = report.Entries["database"].Status == HealthStatus.Healthy;

        return Ok(new
        {
            status = databaseHealthy ? "Healthy" : "Degraded",
            version = buildInfo.Version,
            database = databaseHealthy ? "Healthy" : "Unhealthy"
        });
    }

    [HttpGet("ping")]
    public IActionResult Ping() => Ok(new { status = "Pong", timestampUtc = DateTimeOffset.UtcNow });
}
