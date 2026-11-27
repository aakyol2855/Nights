namespace Nights.Services.Interfaces;

public sealed class TrashItemDto
{
    public int Id { get; init; }
    public string EntityType { get; init; } = string.Empty; // "Memory", "Project", "Note"
    public string EntityTypeDisplay { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Subtitle { get; init; } = string.Empty;
    public string? PreviewImageFileName { get; init; }
    public DateTime DeletedAtUtc { get; init; }
    public string DeletedBy { get; init; } = "Admin";
    public int DaysRemaining { get; init; }
}

public sealed class TrashBinOverviewDto
{
    public int RetentionDays { get; init; } = 30;
    public int MemoryCount { get; init; }
    public int ProjectCount { get; init; }
    public int NoteCount { get; init; }
    public List<TrashItemDto> Items { get; init; } = new();
}

public sealed class TrashPurgeSummaryDto
{
    public int MemoriesPurged { get; init; }
    public int ProjectsPurged { get; init; }
    public int NotesPurged { get; init; }
    public int FilesDeleted { get; init; }
    public int AuditLogsPruned { get; init; }
    public int TotalEntitiesPurged => MemoriesPurged + ProjectsPurged + NotesPurged;
}

/// <summary>
/// Çöp Kutusu (Soft-Delete) geri getirme, kalıcı silme ve otomatik süreli temizlik servisi.
/// </summary>
public interface ITrashService
{
    int RetentionDays { get; }
    int AuditLogRetentionDays { get; }

    Task<TrashBinOverviewDto> GetTrashOverviewAsync(string? entityTypeFilter = null, CancellationToken ct = default);
    Task<(bool Success, string Message)> RestoreAsync(string entityType, int id, string performedBy, CancellationToken ct = default);
    Task<(bool Success, string Message)> HardDeleteAsync(string entityType, int id, string performedBy, CancellationToken ct = default);
    Task<TrashPurgeSummaryDto> PurgeExpiredAsync(CancellationToken ct = default);
}
