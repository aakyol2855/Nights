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
    private readonly ILogger<HomeController> _logger;

    public HomeController(
        IProjectService projectService,
        IMemoryService memoryService,
        ILogger<HomeController> logger)
    {
        _projectService = projectService;
        _memoryService = memoryService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        _logger.LogInformation("[HomeController] Ana sayfa talep edildi.");

        // SQL tarafında limitlenen öne çıkan projeler
        var featuredProjects = await _projectService.GetFeaturedProjectsAsync(3);

        // Cache'li onaylanmış anılar (SQL seviyesinde Take(60) ile optimize edilmiş)
        var memories = await _memoryService.GetApprovedMemoriesAsync();

        ViewBag.FeaturedProjects = featuredProjects;
        ViewBag.RecentMemories = memories.Take(6).ToList();

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
