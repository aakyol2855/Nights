using Markdig;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Nights.Data;
using Nights.Models.Entities;
using Nights.Models.ViewModels;
using Nights.Services.Interfaces;

namespace Nights.Services.Implementations;

/// <summary>
/// Proje ve Rehber (Tutorial) yönetim servisi.
/// Performans & Mimari Kuralları:
/// - AsNoTracking() tüm okuma sorgularında zorunludur.
/// - In-Memory filtering iptal: Sıralama ve filtrelemeler SQL seviyesinde işletilir.
/// - Markdown render sonuçları cache'lenir (Düşük CPU tüketimi).
/// - Proje kartlarında gereksiz ilişkiler çekilmez.
/// </summary>
public class ProjectService : IProjectService
{
    private readonly ApplicationDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ProjectService> _logger;
    private readonly MarkdownPipeline _markdownPipeline;

    private const string ActiveProjectsCacheKey = "cache_active_projects";
    private const string FeaturedProjectsCacheKeyPrefix = "cache_featured_projects_";

    public ProjectService(
        ApplicationDbContext context,
        IMemoryCache cache,
        ILogger<ProjectService> logger)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
        _markdownPipeline = new MarkdownPipelineBuilder()
            .UseAutoLinks()
            .UseEmphasisExtras()
            .Build();
    }

    /// <inheritdoc/>
    public async Task<List<Project>> GetActiveProjectsAsync()
    {
        return await _cache.GetOrCreateAsync(ActiveProjectsCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);

            // AsNoTracking ve SQL seviyesinde filtreleme
            return await _context.Projects
                .AsNoTracking()
                .Where(p => p.IsActive)
                .OrderBy(p => p.DisplayOrder)
                .ThenByDescending(p => p.CreatedAt)
                .Select(p => new Project
                {
                    Id = p.Id,
                    Title = p.Title,
                    Summary = p.Summary,
                    Description = string.Empty, // Listelemede uzun açıklama yüklenmez
                    LiveDemoUrl = p.LiveDemoUrl,
                    GitHubUrl = p.GitHubUrl,
                    ImageUrl = p.ImageUrl,
                    BadgeText = p.BadgeText,
                    TechStack = p.TechStack,
                    DisplayOrder = p.DisplayOrder,
                    IsActive = p.IsActive,
                    IsFeatured = p.IsFeatured,
                    CreatedAt = p.CreatedAt
                })
                .ToListAsync();
        }) ?? new List<Project>();
    }

    /// <inheritdoc/>
    public async Task<List<Project>> GetFeaturedProjectsAsync(int count)
    {
        if (count < 1) count = 3;
        var cacheKey = $"{FeaturedProjectsCacheKeyPrefix}{count}";

        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);

            return await _context.Projects
                .AsNoTracking()
                .Where(p => p.IsActive && p.IsFeatured)
                .OrderBy(p => p.DisplayOrder)
                .ThenByDescending(p => p.CreatedAt)
                .Take(count)
                .Select(p => new Project
                {
                    Id = p.Id,
                    Title = p.Title,
                    Summary = p.Summary,
                    Description = string.Empty,
                    LiveDemoUrl = p.LiveDemoUrl,
                    GitHubUrl = p.GitHubUrl,
                    ImageUrl = p.ImageUrl,
                    BadgeText = p.BadgeText,
                    TechStack = p.TechStack,
                    DisplayOrder = p.DisplayOrder,
                    IsActive = p.IsActive,
                    IsFeatured = p.IsFeatured,
                    CreatedAt = p.CreatedAt
                })
                .ToListAsync();
        }) ?? new List<Project>();
    }

    /// <inheritdoc/>
    public async Task<List<Project>> GetAllProjectsForAdminAsync()
    {
        return await _context.Projects
            .AsNoTracking()
            .OrderBy(p => p.DisplayOrder)
            .ThenByDescending(p => p.CreatedAt)
            .ToListAsync();
    }

    /// <inheritdoc/>
    public async Task<Project?> GetByIdAsync(int id)
    {
        return await _context.Projects
            .AsNoTracking()
            .Include(p => p.Tutorials.OrderBy(t => t.StepOrder))
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    /// <inheritdoc/>
    public async Task<Project?> GetProjectWithTutorialsAsync(int id)
    {
        return await _context.Projects
            .AsNoTracking()
            .Include(p => p.Tutorials.OrderBy(t => t.StepOrder))
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    /// <inheritdoc/>
    public async Task<ProjectDetailViewModel?> GetProjectDetailViewModelAsync(int id)
    {
        var cacheKey = $"cache_project_detail_{id}";

        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(2);

            var project = await _context.Projects
                .AsNoTracking()
                .Include(p => p.Tutorials)
                .FirstOrDefaultAsync(p => p.Id == id && p.IsActive);

            if (project == null) return null;

            var tutorialVms = project.Tutorials
                .OrderBy(t => t.StepOrder)
                .Select(t => new TutorialRenderedItem
                {
                    Id = t.Id,
                    StepTitle = t.StepTitle,
                    StepOrder = t.StepOrder,
                    ContentHtml = Markdown.ToHtml(t.ContentMarkdown ?? string.Empty, _markdownPipeline)
                })
                .ToList();

            return new ProjectDetailViewModel
            {
                Project = project,
                RenderedTutorials = tutorialVms
            };
        });
    }

    /// <inheritdoc/>
    public async Task CreateProjectAsync(Project project)
    {
        _context.Projects.Add(project);
        await _context.SaveChangesAsync();
        InvalidateCache();
        _logger.LogInformation("[ProjectService] Yeni proje oluşturuldu. ProjectId={Id}, Title={Title}", project.Id, project.Title);
    }

    /// <inheritdoc/>
    public async Task UpdateProjectAsync(Project project)
    {
        _context.Projects.Update(project);
        await _context.SaveChangesAsync();
        InvalidateCache(project.Id);
        _logger.LogInformation("[ProjectService] Proje güncellendi. ProjectId={Id}", project.Id);
    }

    /// <inheritdoc/>
    public async Task DeleteProjectAsync(int id)
    {
        var project = await _context.Projects.FindAsync(id);
        if (project != null)
        {
            _context.Projects.Remove(project);
            await _context.SaveChangesAsync();
            InvalidateCache(id);
            _logger.LogInformation("[ProjectService] Proje silindi. ProjectId={Id}", id);
        }
    }

    /// <inheritdoc/>
    public async Task AddTutorialStepAsync(ProjectTutorial tutorial)
    {
        _context.ProjectTutorials.Add(tutorial);
        await _context.SaveChangesAsync();
        InvalidateCache(tutorial.ProjectId);
        _logger.LogInformation("[ProjectService] Yeni tutorial eklendi. ProjectId={ProjectId}", tutorial.ProjectId);
    }

    /// <inheritdoc/>
    public async Task DeleteTutorialStepAsync(int tutorialId)
    {
        var tutorial = await _context.ProjectTutorials.FindAsync(tutorialId);
        if (tutorial != null)
        {
            var projectId = tutorial.ProjectId;
            _context.ProjectTutorials.Remove(tutorial);
            await _context.SaveChangesAsync();
            InvalidateCache(projectId);
            _logger.LogInformation("[ProjectService] Tutorial silindi. TutorialId={Id}", tutorialId);
        }
    }

    /// <inheritdoc/>
    public async Task<int> GetTotalCountAsync()
    {
        return await _context.Projects.AsNoTracking().CountAsync();
    }

    private void InvalidateCache(int? projectId = null)
    {
        _cache.Remove(ActiveProjectsCacheKey);
        _cache.Remove($"{FeaturedProjectsCacheKeyPrefix}3");
        _cache.Remove($"{FeaturedProjectsCacheKeyPrefix}6");

        if (projectId.HasValue)
        {
            _cache.Remove($"cache_project_detail_{projectId.Value}");
        }
    }
}
