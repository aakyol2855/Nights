namespace Nights.Models.Entities;

public class Project : ISoftDeletable
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? LiveDemoUrl { get; set; }
    public string? GitHubUrl { get; set; }
    public string? ImageUrl { get; set; }
    public string BadgeText { get; set; } = "Canlı";
    public string TechStack { get; set; } = ".NET Core, C#";
    public int DisplayOrder { get; set; } = 0;
    public bool IsActive { get; set; } = true;
    public bool IsFeatured { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Docker On-Demand Canlı Demo Orkestrasyon Alanları
    public bool IsDemoEnabled { get; set; } = false;
    public string? DockerImageName { get; set; }
    public int ContainerInternalPort { get; set; } = 8080;
    public string? EnvironmentVariables { get; set; }
    public bool UseSharedDemoNetwork { get; set; } = true;
    public string? MarkdownGuide { get; set; }

    // Soft-Delete (Çöp Kutusu)
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }

    public ICollection<ProjectTutorial> Tutorials { get; set; } = new List<ProjectTutorial>();
    public ICollection<ActiveDemoSession> DemoSessions { get; set; } = new List<ActiveDemoSession>();
}
