namespace Nights.Models.Entities;

public class ProjectTutorial
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public Project? Project { get; set; }
    public string StepTitle { get; set; } = string.Empty;
    public string ContentMarkdown { get; set; } = string.Empty;
    public int StepOrder { get; set; } = 1;
}
