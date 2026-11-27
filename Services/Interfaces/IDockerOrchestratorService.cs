namespace Nights.Services.Interfaces;

/// <summary>
/// Aktif demo oturumunun bellek içi hafif anlık görüntüsü (YARP reverse proxy için sıfır DB yükü).
/// </summary>
public sealed class ActiveDemoSnapshot
{
    public int SessionId { get; init; }
    public int ProjectId { get; init; }
    public string ProjectTitle { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string ContainerId { get; init; } = string.Empty;
    public string ContainerName { get; init; } = string.Empty;
    public string ImageName { get; init; } = string.Empty;
    public int HostPort { get; init; }
    public int ContainerPort { get; init; }
    public DateTime StartedAtUtc { get; init; }
    public DateTime LastActivityUtc { get; set; }
}

public sealed class DemoStartResult
{
    public bool Success { get; init; }
    public bool IsBusy { get; init; }
    public string? DemoUrl { get; init; }
    public string? Slug { get; init; }
    public string? ActiveProjectTitle { get; init; }
    public int RemainingSeconds { get; init; }
    public string Message { get; init; } = string.Empty;
}

public sealed class DemoLoadImageResult
{
    public bool Success { get; init; }
    public string? LoadedImageName { get; init; }
    public string Message { get; init; } = string.Empty;
}

/// <summary>
/// Donanım dostu, Command Injection korumalı On-Demand Docker Demo Orkestrasyon servisi.
/// Aynı anda en fazla 1 container çalıştırır ve 15 dk hareketsizlikte otomatik temizler.
/// </summary>
public interface IDockerOrchestratorService
{
    TimeSpan IdleTimeout { get; }

    Task<DemoLoadImageResult> LoadImageFromTarAsync(Stream tarStream, string originalFileName, CancellationToken ct = default);
    Task<DemoStartResult> StartDemoAsync(int projectId, CancellationToken ct = default);
    Task<bool> StopActiveDemoAsync(string reason = "ManualStop", CancellationToken ct = default);
    Task CleanupIdleDemoAsync(CancellationToken ct = default);
    Task CleanupOrphanedContainersAsync(CancellationToken ct = default);

    ActiveDemoSnapshot? GetActiveSessionBySlug(string slug);
    ActiveDemoSnapshot? GetCurrentActiveSession();
    void TouchActivity(string slug);
}
