using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Nights.Services.Interfaces;

namespace Nights.Controllers;

public class ProjectsController : Controller
{
    private readonly IProjectService _projectService;
    private readonly ILogger<ProjectsController> _logger;

    public ProjectsController(
        IProjectService projectService,
        ILogger<ProjectsController> logger)
    {
        _projectService = projectService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        _logger.LogInformation("[ProjectsController] Projeler listesi görüntülendi.");
        var projects = await _projectService.GetActiveProjectsAsync();
        return View(projects);
    }

    [HttpGet]
    public async Task<IActionResult> Detail(int id)
    {
        _logger.LogInformation("[ProjectsController] Proje detayı talep edildi. ProjectId={Id}", id);
        var viewModel = await _projectService.GetProjectDetailViewModelAsync(id);
        if (viewModel == null)
        {
            _logger.LogWarning("[ProjectsController] Proje bulunamadı veya pasif durumda. ProjectId={Id}", id);
            return NotFound();
        }

        return View(viewModel);
    }
}
