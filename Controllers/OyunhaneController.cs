using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Nights.Models.ViewModels;
using Nights.Services.Games;

namespace Nights.Controllers;

public class OyunhaneController : Controller
{
    private readonly IGameRegistryService _gameRegistry;
    private readonly IGameScoreService _scoreService;
    private readonly ILogger<OyunhaneController> _logger;

    public OyunhaneController(
        IGameRegistryService gameRegistry,
        IGameScoreService scoreService,
        ILogger<OyunhaneController> logger)
    {
        _gameRegistry = gameRegistry;
        _scoreService = scoreService;
        _logger = logger;
    }

    [HttpGet("Oyunhane")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var games = _gameRegistry.GetActiveGames();
        var topTier = await _scoreService.GetGlobalTopTierListAsync(User, HttpContext.Session, ct);

        var vm = new OyunhaneIndexViewModel
        {
            Games = games,
            GlobalTopTier = topTier,
            IsAuthenticated = User.Identity?.IsAuthenticated == true
        };

        return View(vm);
    }

    [HttpGet("Oyunhane/Play/{slug}")]
    public async Task<IActionResult> Play(string slug, CancellationToken ct)
    {
        var module = _gameRegistry.GetBySlug(slug);
        if (module == null)
        {
            _logger.LogWarning("[OyunhaneController] İstenen oyun bulunamadı: {Slug}", slug);
            return NotFound();
        }

        var defaultDiff = module.Difficulties.FirstOrDefault()?.Key ?? "all";
        var initialBoard = await _scoreService.GetGameLeaderboardAsync(module.Slug, defaultDiff, User, HttpContext.Session, ct);
        var isAuthenticated = User.Identity?.IsAuthenticated == true;

        var vm = new OyunhanePlayViewModel
        {
            Game = module,
            InitialLeaderboard = initialBoard,
            IsAuthenticated = isAuthenticated,
            SafeCurrentUserName = isAuthenticated ? GameScoreService.SanitizeRegisteredUserName(User.Identity?.Name) : string.Empty
        };

        return View(vm);
    }
}
