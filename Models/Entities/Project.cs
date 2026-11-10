namespace Nights.Models.Entities;

public class Project
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
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

    public ICollection<ProjectTutorial> Tutorials { get; set; } = new List<ProjectTutorial>();
}
