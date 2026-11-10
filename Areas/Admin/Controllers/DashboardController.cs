using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Nights.Models.ViewModels;
using Nights.Services.Interfaces;

namespace Nights.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize]
public class DashboardController : Controller
{
    private readonly IMemoryService _memoryService;
    private readonly IProjectService _projectService;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(
        IMemoryService memoryService,
        IProjectService projectService,
        ILogger<DashboardController> logger)
    {
        _memoryService = memoryService;
        _projectService = projectService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        _logger.LogInformation("[DashboardController] Yönetim paneli özeti görüntülendi.");

        var (pending, approved) = await _memoryService.GetCountsAsync();
        var projectsCount = await _projectService.GetTotalCountAsync();

        // SQL tarafında TOP 5 limitli beklemedeki anılar
        var recentPending = await _memoryService.GetRecentPendingAsync(5);

        var vm = new AdminDashboardViewModel
        {
            PendingMemoriesCount = pending,
            ApprovedMemoriesCount = approved,
            ProjectsCount = projectsCount,
            RecentPendingMemories = recentPending
        };

        return View(vm);
    }
}
