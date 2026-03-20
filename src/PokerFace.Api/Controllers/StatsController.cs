using Microsoft.AspNetCore.Mvc;
using PokerFace.Api.Services;
using PokerFace.Shared;

namespace PokerFace.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StatsController : ControllerBase
{
    private readonly AppStatsService _statsService;

    public StatsController(AppStatsService statsService)
    {
        _statsService = statsService;
    }

    [HttpGet]
    public ActionResult<AppStatsDto> GetStats()
    {
        var stats = _statsService.GetStats();
        return Ok(new AppStatsDto
        {
            TotalTablesCreated = stats.TotalTablesCreated,
            TotalPlayersConnected = stats.TotalPlayersConnected,
            MaxSimultaneousUsersAtTable = stats.MaxSimultaneousUsersAtTable
        });
    }
}
