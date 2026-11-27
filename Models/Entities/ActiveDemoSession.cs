namespace Nights.Models.Entities;

/// <summary>
/// On-Demand (isteğe bağlı) başlatılan canlı Docker demo container oturumunu takip eder.
/// </summary>
public class ActiveDemoSession
{
    public int Id { get; set; }

    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public string Slug { get; set; } = string.Empty;
    public string ContainerId { get; set; } = string.Empty;
    public string ContainerName { get; set; } = string.Empty;
    public string ImageName { get; set; } = string.Empty;

    public int HostPort { get; set; }
    public int ContainerPort { get; set; }

    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastActivityUtc { get; set; } = DateTime.UtcNow;

    public bool IsActive { get; set; } = true;
    public string? StoppedReason { get; set; }
    public DateTime? StoppedAtUtc { get; set; }
}
