using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Nights.Services.Games;

namespace Nights.Controllers.Api;

[ApiController]
[Route("api/games")]
public class GamesApiController : ControllerBase
{
    private readonly IGameScoreService _scoreService;
    private readonly IGameRegistryService _registry;

    public GamesApiController(IGameScoreService scoreService, IGameRegistryService registry)
    {
        _scoreService = scoreService;
        _registry = registry;
    }

    [HttpGet("{slug}/leaderboard")]
    public async Task<IActionResult> GetLeaderboard(
        string slug,
        [FromQuery] string? difficulty,
        CancellationToken ct)
    {
        var module = _registry.GetBySlug(slug);
        if (module == null)
        {
            return NotFound(new { success = false, message = "Oyun bulunamadı." });
        }

        var entries = await _scoreService.GetGameLeaderboardAsync(module.Slug, difficulty, User, HttpContext.Session, ct);
        return Ok(new
        {
            success = true,
            gameSlug = module.Slug,
            difficulty = string.IsNullOrWhiteSpace(difficulty) ? "all" : difficulty,
            entries
        });
    }

    [HttpGet("top-tier")]
    public async Task<IActionResult> GetGlobalTopTier(CancellationToken ct)
    {
        var entries = await _scoreService.GetGlobalTopTierListAsync(User, HttpContext.Session, ct);
        return Ok(new
        {
            success = true,
            entries
        });
    }

    [HttpPost("{slug}/score")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("game-score-policy")]
    public async Task<IActionResult> SubmitScore(
        string slug,
        [FromBody] GameScoreSubmissionDto submission,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new { success = false, message = "Geçersiz skor verisi." });
        }

        var result = await _scoreService.SubmitScoreAsync(slug, submission, User, HttpContext.Session, ct);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        var updatedBoard = await _scoreService.GetGameLeaderboardAsync(slug, submission.Difficulty, User, HttpContext.Session, ct);
        return Ok(new
        {
            result.Success,
            result.Message,
            result.FinalScore,
            result.CurrentRank,
            result.EarnedTierPoints,
            result.IsPersonalBest,
            result.IsSavedPermanently,
            result.IsGuestSession,
            result.DisplayName,
            leaderboard = updatedBoard
        });
    }
}
