namespace Nights.Models.Entities;

public class Note
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public bool IsAdminNote { get; set; } = false;
    public bool IsPinned { get; set; } = false;

    public int WordCount { get; set; } = 0;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Soft-delete bayrağı. true ise not listelerinde gösterilmez; fiziksel satır silinmez.
    /// </summary>
    public bool IsDeleted { get; set; } = false;
}
