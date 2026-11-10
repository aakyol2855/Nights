namespace Nights.Models.Entities;

public class Memory
{
    public int Id { get; set; }
    public string Description { get; set; } = string.Empty;
    public string ImageFileName { get; set; } = string.Empty;
    public bool IsApproved { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string SubmitterIpHash { get; set; } = string.Empty;
}
