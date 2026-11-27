using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Nights.Models;
using Nights.Services.Interfaces;

namespace Nights.Controllers;

public class HomeController : Controller
{
    private readonly IProjectService _projectService;
    private readonly IMemoryService _memoryService;
    private readonly ISystemHealthService _healthService;
    private readonly INoteService _noteService;
    private readonly ILogger<HomeController> _logger;

    public HomeController(
        IProjectService projectService,
        IMemoryService memoryService,
        ISystemHealthService healthService,
        INoteService noteService,
        ILogger<HomeController> logger)
    {
        _projectService = projectService;
        _memoryService = memoryService;
        _healthService = healthService;
        _noteService = noteService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        _logger.LogInformation("[HomeController] Divan Ana Sayfası talep edildi.");

        var featuredProjects = await _projectService.GetFeaturedProjectsAsync(3);
        var memories = await _memoryService.GetApprovedMemoriesAsync();
        var healthStats = await _healthService.GetHealthStatsAsync();
        var (notes, _, _) = await _noteService.GetNotesPagedAsync(1, 3);

        ViewBag.FeaturedProjects = featuredProjects;
        ViewBag.RecentMemories = memories.Take(4).ToList();
        ViewBag.HealthStats = healthStats;
        ViewBag.RecentNotes = notes;

        return View();
    }

    [HttpGet]
    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        _logger.LogError("[HomeController] Hata sayfası gösteriliyor. TraceId: {TraceId}", Activity.Current?.Id ?? HttpContext.TraceIdentifier);
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
