using Microsoft.AspNetCore.Mvc;
using Nights.Services.Interfaces;

namespace Nights.Controllers.Api;

[ApiController]
[Route("api/health-stats")]
[ResponseCache(Duration = 30, Location = ResponseCacheLocation.Client)]
public class HealthStatsController : ControllerBase
{
    private readonly ISystemHealthService _healthService;

    public HealthStatsController(ISystemHealthService healthService)
    {
        _healthService = healthService;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var stats = await _healthService.GetHealthStatsAsync();
        return Ok(stats);
    }
}
