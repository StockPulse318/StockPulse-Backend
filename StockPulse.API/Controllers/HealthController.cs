using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace StockPulse.API.Controllers;

[ApiController]
[Route("health")]
[Route("api/health")]
[AllowAnonymous]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Check()
    {
        return Ok(new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow.ToString("o")
        });
    }
}
