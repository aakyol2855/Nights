using Nights.Models.Entities;

namespace Nights.Models.ViewModels;

public class ProjectListViewModel
{
    public List<Project> Projects { get; set; } = new();
}

public class ProjectDetailViewModel
{
    public Project Project { get; set; } = null!;
    public string? MarkdownGuideHtml { get; set; }
    public List<TutorialRenderedItem> RenderedTutorials { get; set; } = new();
}

public class TutorialRenderedItem
{
    public int Id { get; set; }
    public string StepTitle { get; set; } = string.Empty;
    public string ContentHtml { get; set; } = string.Empty;
    public int StepOrder { get; set; }
}
