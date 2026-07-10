using Microsoft.AspNetCore.Mvc;

namespace ExcelETL.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new { status = "Healthy" });

    [HttpGet("ping")]
    public IActionResult Ping() => Ok(new { status = "Pong", timestampUtc = DateTimeOffset.UtcNow });
}
