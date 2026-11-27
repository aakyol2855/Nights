using Nights.Services.Interfaces;

namespace Nights.Services.Background;

/// <summary>
/// 15 dakika boyunca istek almayan canlı demo container'larını otomatik olarak durdurup silen
/// düşük kaynak tüketimli arka plan servisi (Auto-Sleep & Orphan Cleanup).
/// </summary>
public sealed class DemoCleanupHostedService : BackgroundService
{
    private readonly IDockerOrchestratorService _orchestrator;
    private readonly ILogger<DemoCleanupHostedService> _logger;

    public DemoCleanupHostedService(
        IDockerOrchestratorService orchestrator,
        ILogger<DemoCleanupHostedService> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[DemoCleanupHostedService] Arka plan demo temizlik servisi başlatıldı.");

        // Uygulama açılışında önceki oturumdan kalmış yetim container'ları temizle
        await _orchestrator.CleanupOrphanedContainersAsync(stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken))
                    break;

                await _orchestrator.CleanupIdleDemoAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[DemoCleanupHostedService] Periyodik temizlik sırasında hata oluştu.");
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("[DemoCleanupHostedService] Uygulama kapanıyor, aktif demo container durduruluyor...");
        try
        {
            await _orchestrator.StopActiveDemoAsync("ApplicationShutdown", cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[DemoCleanupHostedService] Kapanış sırasında demo durdurulamadı.");
        }

        await base.StopAsync(cancellationToken);
    }
}
