using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Nights.Models.Entities;

namespace Nights.Models.ViewModels;

public class AdminDashboardViewModel
{
    public int PendingMemoriesCount { get; set; }
    public int ApprovedMemoriesCount { get; set; }
    public int ProjectsCount { get; set; }
    public List<Memory> RecentPendingMemories { get; set; } = new();
}

public class ProjectEditViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Proje başlığı zorunludur.")]
    [StringLength(100)]
    public string Title { get; set; } = string.Empty;

    [StringLength(120)]
    public string? Slug { get; set; }

    [Required(ErrorMessage = "Kısa özet zorunludur.")]
    [StringLength(300)]
    public string Summary { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    [Url(ErrorMessage = "Geçerli bir URL giriniz.")]
    public string? LiveDemoUrl { get; set; }

    [Url(ErrorMessage = "Geçerli bir URL giriniz.")]
    public string? GitHubUrl { get; set; }

    public string? ImageUrl { get; set; }

    [StringLength(50)]
    public string BadgeText { get; set; } = "Canlı";

    [StringLength(100)]
    public string TechStack { get; set; } = ".NET, C#";

    public int DisplayOrder { get; set; } = 0;
    public bool IsActive { get; set; } = true;
    public bool IsFeatured { get; set; } = false;

    // Docker On-Demand Canlı Demo Alanları
    public bool IsDemoEnabled { get; set; } = false;

    [StringLength(256)]
    public string? DockerImageName { get; set; }

    [Range(1, 65535, ErrorMessage = "Container portu 1 ile 65535 arasında olmalıdır.")]
    public int ContainerInternalPort { get; set; } = 8080;

    [StringLength(4000)]
    public string? EnvironmentVariables { get; set; }

    public bool UseSharedDemoNetwork { get; set; } = true;

    public string? MarkdownGuide { get; set; }

    public IFormFile? DockerImageTarFile { get; set; }
}

public class TutorialEditViewModel
{
    public int Id { get; set; }
    public int ProjectId { get; set; }

    [Required(ErrorMessage = "Adım başlığı zorunludur.")]
    [StringLength(150)]
    public string StepTitle { get; set; } = string.Empty;

    [Required(ErrorMessage = "Markdown içerik zorunludur.")]
    public string ContentMarkdown { get; set; } = string.Empty;

    public int StepOrder { get; set; } = 1;
}
