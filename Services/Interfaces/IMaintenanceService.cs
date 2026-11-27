namespace Nights.Services.Interfaces;

public sealed class OrphanFileItemDto
{
    public string Folder { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
    public DateTime LastWriteUtc { get; init; }
}

public sealed class PodmanHealthDiagnosticsDto
{
    public string CliPath { get; init; } = string.Empty;
    public bool IsCliAvailable { get; init; }
    public string CliVersion { get; init; } = string.Empty;
    public string SharedNetworkName { get; init; } = "divan_demo_net";
    public bool IsSharedNetworkPresent { get; init; }
    public List<string> OrphanContainers { get; init; } = new();
    public string? ActiveDemoName { get; init; }
    public double DiskUsagePercent { get; init; }
    public string DiskFreeFormatted { get; init; } = string.Empty;
    public string DiskTotalFormatted { get; init; } = string.Empty;
}

public sealed class DatabaseStatsDto
{
    public int ActiveMemories { get; init; }
    public int ActiveProjects { get; init; }
    public int ActiveNotes { get; init; }
    public int TotalTrashItems { get; init; }
    public int TotalAuditLogs { get; init; }
    public int TotalDemoSessions { get; init; }
    public string DatabaseSizeFormatted { get; init; } = "Bilinmiyor";
}

public sealed class MaintenanceDiagnosticsDto
{
    public List<OrphanFileItemDto> OrphanFiles { get; init; } = new();
    public long TotalOrphanBytes => OrphanFiles.Sum(f => f.SizeBytes);
    public PodmanHealthDiagnosticsDto PodmanHealth { get; init; } = new();
    public DatabaseStatsDto DatabaseStats { get; init; } = new();
    public DateTime GeneratedAtUtc { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Veri Bakım, Yetim Dosya Temizliği, Podman Sağlık Teşhisi ve Önbellek Yönetimi servisi.
/// </summary>
public interface IMaintenanceService
{
    Task<MaintenanceDiagnosticsDto> GetDiagnosticsAsync(CancellationToken ct = default);
    Task<(int DeletedCount, long FreedBytes)> CleanOrphanFilesAsync(string performedBy, CancellationToken ct = default);
    Task<int> CleanOrphanContainersAsync(string performedBy, CancellationToken ct = default);
    Task PurgeMemoryCacheAsync(string performedBy, CancellationToken ct = default);
}
