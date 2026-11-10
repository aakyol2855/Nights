using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Nights.Models.Entities;
using Nights.Models.ViewModels;
using Nights.Services.Interfaces;

namespace Nights.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize]
public class AdminProjectsController : Controller
{
    private readonly IProjectService _projectService;
    private readonly ILogger<AdminProjectsController> _logger;

    public AdminProjectsController(
        IProjectService projectService,
        ILogger<AdminProjectsController> logger)
    {
        _projectService = projectService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        _logger.LogInformation("[AdminProjectsController] Yönetici proje listesi görüntülendi.");
        var projects = await _projectService.GetAllProjectsForAdminAsync();
        return View(projects);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new ProjectEditViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ProjectEditViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var project = new Project
        {
            Title = model.Title.Trim(),
            Summary = model.Summary.Trim(),
            Description = model.Description?.Trim() ?? string.Empty,
            LiveDemoUrl = model.LiveDemoUrl?.Trim(),
            GitHubUrl = model.GitHubUrl?.Trim(),
            ImageUrl = model.ImageUrl?.Trim(),
            BadgeText = string.IsNullOrWhiteSpace(model.BadgeText) ? "Canlı" : model.BadgeText.Trim(),
            TechStack = string.IsNullOrWhiteSpace(model.TechStack) ? ".NET, C#" : model.TechStack.Trim(),
            DisplayOrder = model.DisplayOrder,
            IsActive = model.IsActive,
            IsFeatured = model.IsFeatured,
            CreatedAt = DateTime.UtcNow
        };

        await _projectService.CreateProjectAsync(project);
        _logger.LogInformation("[AdminProjectsController] Yeni proje eklendi. Title={Title}", project.Title);
        TempData["SuccessMessage"] = "Proje başarıyla eklendi.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var project = await _projectService.GetByIdAsync(id);
        if (project == null)
        {
            _logger.LogWarning("[AdminProjectsController] Düzenlenecek proje bulunamadı. Id={Id}", id);
            return NotFound();
        }

        ViewBag.Tutorials = project.Tutorials.OrderBy(t => t.StepOrder).ToList();

        var vm = new ProjectEditViewModel
        {
            Id = project.Id,
            Title = project.Title,
            Summary = project.Summary,
            Description = project.Description,
            LiveDemoUrl = project.LiveDemoUrl,
            GitHubUrl = project.GitHubUrl,
            ImageUrl = project.ImageUrl,
            BadgeText = project.BadgeText,
            TechStack = project.TechStack,
            DisplayOrder = project.DisplayOrder,
            IsActive = project.IsActive,
            IsFeatured = project.IsFeatured
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ProjectEditViewModel model)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            var project = await _projectService.GetByIdAsync(id);
            if (project != null)
            {
                ViewBag.Tutorials = project.Tutorials.OrderBy(t => t.StepOrder).ToList();
            }
            return View(model);
        }

        var existing = await _projectService.GetByIdAsync(id);
        if (existing == null)
        {
            return NotFound();
        }

        existing.Title = model.Title.Trim();
        existing.Summary = model.Summary.Trim();
        existing.Description = model.Description?.Trim() ?? string.Empty;
        existing.LiveDemoUrl = model.LiveDemoUrl?.Trim();
        existing.GitHubUrl = model.GitHubUrl?.Trim();
        existing.ImageUrl = model.ImageUrl?.Trim();
        existing.BadgeText = string.IsNullOrWhiteSpace(model.BadgeText) ? "Canlı" : model.BadgeText.Trim();
        existing.TechStack = model.TechStack.Trim();
        existing.DisplayOrder = model.DisplayOrder;
        existing.IsActive = model.IsActive;
        existing.IsFeatured = model.IsFeatured;

        await _projectService.UpdateProjectAsync(existing);
        _logger.LogInformation("[AdminProjectsController] Proje güncellendi. Id={Id}", id);
        TempData["SuccessMessage"] = "Proje güncellendi.";
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddTutorial(TutorialEditViewModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Rehber adımı eklenirken form hatası oluştu.";
            return RedirectToAction(nameof(Edit), new { id = model.ProjectId });
        }

        var tutorial = new ProjectTutorial
        {
            ProjectId = model.ProjectId,
            StepTitle = model.StepTitle.Trim(),
            ContentMarkdown = model.ContentMarkdown.Trim(),
            StepOrder = model.StepOrder
        };

        await _projectService.AddTutorialStepAsync(tutorial);
        _logger.LogInformation("[AdminProjectsController] Yeni rehber adımı eklendi. ProjectId={ProjectId}", model.ProjectId);
        TempData["SuccessMessage"] = "Rehber adımı başarıyla eklendi.";
        return RedirectToAction(nameof(Edit), new { id = model.ProjectId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteTutorial(int tutorialId, int projectId)
    {
        await _projectService.DeleteTutorialStepAsync(tutorialId);
        _logger.LogInformation("[AdminProjectsController] Rehber adımı silindi. TutorialId={Id}", tutorialId);
        TempData["SuccessMessage"] = "Rehber adımı silindi.";
        return RedirectToAction(nameof(Edit), new { id = projectId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        await _projectService.DeleteProjectAsync(id);
        _logger.LogInformation("[AdminProjectsController] Proje silindi. ProjectId={Id}", id);
        return Json(new { success = true });
    }
}
