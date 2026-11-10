using System.ComponentModel.DataAnnotations;
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
