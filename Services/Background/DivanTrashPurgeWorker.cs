using Nights.Services.Interfaces;

namespace Nights.Services.Background;

/// <summary>
/// Eski Debian ev sunucusu için düşük maliyetli arka plan temizlik işçisi.
/// 12 saatte bir çalışarak:
/// 1) Saklama süresi (varsayılan 30 gün) dolan soft-delete kayıtlarını ve diskteki görsellerini temizler.
/// 2) 60 günden eski sistem denetim kayıtlarını budayarak veritabanı şişmesini önler.
/// </summary>
public sealed class DivanTrashPurgeWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DivanTrashPurgeWorker> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromHours(12);

    public DivanTrashPurgeWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<DivanTrashPurgeWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[DivanTrashPurgeWorker] Çöp kutusu ve log budama servisi başlatıldı (Periyot: 12 saat).");

        // Uygulama açılışında 2 dakika bekle ki başlangıç yükü oluşmasın
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        using var timer = new PeriodicTimer(Interval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var trashService = scope.ServiceProvider.GetRequiredService<ITrashService>();
                await trashService.PurgeExpiredAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[DivanTrashPurgeWorker] Periyodik temizlik sırasında hata oluştu.");
            }

            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken))
                {
                    break;
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("[DivanTrashPurgeWorker] Servis durduruldu.");
    }
}
