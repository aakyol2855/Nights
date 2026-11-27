using Nights.Models.Entities;
using Nights.Models.ViewModels;

namespace Nights.Services.Interfaces;

public interface IProjectService
{
    /// <summary>Aktif projelerin tamamını döndürür (cache'li, 1 saat).</summary>
    Task<List<Project>> GetActiveProjectsAsync();

    /// <summary>
    /// Ana sayfa için öne çıkan N projeyi döndürür.
    /// SQL tarafında IsFeatured + Take uygulanır — in-memory filtering yok.
    /// </summary>
    Task<List<Project>> GetFeaturedProjectsAsync(int count = 3);

    /// <summary>Tüm projeleri admin paneli için (Tutorial'lar dahil) döndürür.</summary>
    Task<List<Project>> GetAllProjectsForAdminAsync();

    Task<Project?> GetProjectWithTutorialsAsync(int id);
    Task<ProjectDetailViewModel?> GetProjectDetailViewModelAsync(int id);
    Task<Project?> GetByIdAsync(int id);
    Task CreateProjectAsync(Project project);
    Task UpdateProjectAsync(Project project);
    Task DeleteProjectAsync(int id, string? deletedBy = null);
    Task AddTutorialStepAsync(ProjectTutorial tutorial);
    Task DeleteTutorialStepAsync(int tutorialId);
    Task<int> GetTotalCountAsync();
}
