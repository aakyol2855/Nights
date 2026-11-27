using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Nights.Models.Entities;
using Nights.Models.ViewModels;
using Nights.Services.Implementations;
using Nights.Services.Interfaces;

namespace Nights.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class AdminProjectsController : Controller
{
    private readonly IProjectService _projectService;
    private readonly IDockerOrchestratorService _orchestrator;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<AdminProjectsController> _logger;

    public AdminProjectsController(
        IProjectService projectService,
        IDockerOrchestratorService orchestrator,
        IAuditLogService auditLogService,
        ILogger<AdminProjectsController> logger)
    {
        _projectService = projectService;
        _orchestrator = orchestrator;
        _auditLogService = auditLogService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        _logger.LogInformation("[AdminProjectsController] Yönetici proje listesi görüntülendi.");
        var projects = await _projectService.GetAllProjectsForAdminAsync();
        ViewBag.ActiveDemo = _orchestrator.GetCurrentActiveSession();
        return View(projects);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StopActiveDemo(CancellationToken ct)
    {
        var stopped = await _orchestrator.StopActiveDemoAsync("AdminManualStop", ct);
        TempData["SuccessMessage"] = stopped
            ? "Aktif demo container başarıyla durduruldu ve bellek serbest bırakıldı."
            : "Şu anda çalışan bir demo container bulunmuyor.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new ProjectEditViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestFormLimits(MultipartBodyLengthLimit = 524_288_000)]
    [RequestSizeLimit(524_288_000)]
    public async Task<IActionResult> Create(ProjectEditViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var dockerImageName = model.DockerImageName?.Trim();

        if (model.DockerImageTarFile != null && model.DockerImageTarFile.Length > 0)
        {
            if (!model.DockerImageTarFile.FileName.EndsWith(".tar", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(nameof(model.DockerImageTarFile), "Yalnızca '.tar' uzantılı Docker imaj arşivleri yüklenebilir.");
                return View(model);
            }

            await using var stream = model.DockerImageTarFile.OpenReadStream();
            var loadResult = await _orchestrator.LoadImageFromTarAsync(stream, model.DockerImageTarFile.FileName, ct);
            if (!loadResult.Success)
            {
                ModelState.AddModelError(nameof(model.DockerImageTarFile), loadResult.Message);
                return View(model);
            }

            if (!string.IsNullOrWhiteSpace(loadResult.LoadedImageName))
            {
                dockerImageName = loadResult.LoadedImageName;
            }
            TempData["SuccessMessage"] = loadResult.Message;
        }

        var project = new Project
        {
            Title = model.Title.Trim(),
            Slug = DockerOrchestratorService.NormalizeSlug(string.IsNullOrWhiteSpace(model.Slug) ? model.Title : model.Slug, 0),
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
            IsDemoEnabled = model.IsDemoEnabled,
            DockerImageName = dockerImageName,
            ContainerInternalPort = model.ContainerInternalPort is >= 1 and <= 65535 ? model.ContainerInternalPort : 8080,
            EnvironmentVariables = model.EnvironmentVariables?.Trim(),
            UseSharedDemoNetwork = model.UseSharedDemoNetwork,
            MarkdownGuide = model.MarkdownGuide?.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        await _projectService.CreateProjectAsync(project);
        _logger.LogInformation("[AdminProjectsController] Yeni proje eklendi. Title={Title}, Demo={Demo}", project.Title, project.IsDemoEnabled);
        TempData["SuccessMessage"] ??= "Proje başarıyla eklendi.";
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
            Slug = project.Slug,
            Summary = project.Summary,
            Description = project.Description,
            LiveDemoUrl = project.LiveDemoUrl,
            GitHubUrl = project.GitHubUrl,
            ImageUrl = project.ImageUrl,
            BadgeText = project.BadgeText,
            TechStack = project.TechStack,
            DisplayOrder = project.DisplayOrder,
            IsActive = project.IsActive,
            IsFeatured = project.IsFeatured,
            IsDemoEnabled = project.IsDemoEnabled,
            DockerImageName = project.DockerImageName,
            ContainerInternalPort = project.ContainerInternalPort is >= 1 and <= 65535 ? project.ContainerInternalPort : 8080,
            EnvironmentVariables = project.EnvironmentVariables,
            UseSharedDemoNetwork = project.UseSharedDemoNetwork,
            MarkdownGuide = project.MarkdownGuide
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestFormLimits(MultipartBodyLengthLimit = 524_288_000)]
    [RequestSizeLimit(524_288_000)]
    public async Task<IActionResult> Edit(int id, ProjectEditViewModel model, CancellationToken ct)
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

        var dockerImageName = model.DockerImageName?.Trim();

        if (model.DockerImageTarFile != null && model.DockerImageTarFile.Length > 0)
        {
            if (!model.DockerImageTarFile.FileName.EndsWith(".tar", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(nameof(model.DockerImageTarFile), "Yalnızca '.tar' uzantılı Docker imaj arşivleri yüklenebilir.");
                ViewBag.Tutorials = existing.Tutorials.OrderBy(t => t.StepOrder).ToList();
                return View(model);
            }

            await using var stream = model.DockerImageTarFile.OpenReadStream();
            var loadResult = await _orchestrator.LoadImageFromTarAsync(stream, model.DockerImageTarFile.FileName, ct);
            if (!loadResult.Success)
            {
                ModelState.AddModelError(nameof(model.DockerImageTarFile), loadResult.Message);
                ViewBag.Tutorials = existing.Tutorials.OrderBy(t => t.StepOrder).ToList();
                return View(model);
            }

            if (!string.IsNullOrWhiteSpace(loadResult.LoadedImageName))
            {
                dockerImageName = loadResult.LoadedImageName;
            }
            TempData["SuccessMessage"] = loadResult.Message;
        }

        existing.Title = model.Title.Trim();
        existing.Slug = DockerOrchestratorService.NormalizeSlug(string.IsNullOrWhiteSpace(model.Slug) ? model.Title : model.Slug, existing.Id);
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
        existing.IsDemoEnabled = model.IsDemoEnabled;
        existing.DockerImageName = dockerImageName;
        existing.ContainerInternalPort = model.ContainerInternalPort is >= 1 and <= 65535 ? model.ContainerInternalPort : 8080;
        existing.EnvironmentVariables = model.EnvironmentVariables?.Trim();
        existing.UseSharedDemoNetwork = model.UseSharedDemoNetwork;
        existing.MarkdownGuide = model.MarkdownGuide?.Trim();

        await _projectService.UpdateProjectAsync(existing);
        _logger.LogInformation("[AdminProjectsController] Proje güncellendi. Id={Id}", id);
        TempData["SuccessMessage"] ??= "Proje güncellendi.";
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
        await _projectService.DeleteProjectAsync(id, User.Identity?.Name);
        await _auditLogService.LogAsync(
            category: "ItemSoftDeleted",
            summary: $"Proje #{id} Çöp Kutusu'na taşındı (30 gün saklama).",
            targetEntityType: "Project",
            targetEntityId: id.ToString(),
            logLevel: "Warning",
            username: User.Identity?.Name,
            component: "AdminProjectsController");
        _logger.LogInformation("[AdminProjectsController] Proje çöp kutusuna taşındı. ProjectId={Id}", id);
        return Json(new { success = true });
    }
}
