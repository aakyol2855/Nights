using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Nights.Data;
using Nights.Models.Entities;
using Nights.Services.Interfaces;

namespace Nights.Services.Implementations;

/// <summary>
/// Eski Debian Linux donanımı için optimize edilmiş, Command Injection korumalı
/// On-Demand Docker/Podman Demo Orkestrasyon servisi.
/// </summary>
public sealed partial class DockerOrchestratorService : IDockerOrchestratorService
{
    private const int MinHostPort = 5100;
    private const int MaxHostPort = 5199;
    private const string DefaultSharedDemoNetwork = "divan_demo_net";
    private const string ContainerNamePrefix = "divan-demo-";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DockerOrchestratorService> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private volatile ActiveDemoSnapshot? _activeSession;
    private DateTime _lastDbActivitySyncUtc = DateTime.MinValue;
    private readonly string _containerCli;
    private readonly string _sharedDemoNetwork;
    private readonly string _memoryLimit;
    private readonly string _cpuLimit;
    private readonly TimeSpan _startupTimeout;

    public TimeSpan IdleTimeout { get; }

    [GeneratedRegex(@"^[a-zA-Z0-9][a-zA-Z0-9._/:@-]{0,200}$")]
    private static partial Regex ValidImageNameRegex();

    [GeneratedRegex(@"^[a-z0-9][a-z0-9-]{0,80}$")]
    private static partial Regex ValidSlugRegex();

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex ValidEnvKeyRegex();

    public DockerOrchestratorService(
        IServiceScopeFactory scopeFactory,
        IWebHostEnvironment env,
        IConfiguration configuration,
        ILogger<DockerOrchestratorService> logger)
    {
        _scopeFactory = scopeFactory;
        _env = env;
        _configuration = configuration;
        _logger = logger;

        var idleMinutes = _configuration.GetValue<int?>("DemoOrchestrator:IdleTimeoutMinutes") ?? 15;
        IdleTimeout = TimeSpan.FromMinutes(Math.Clamp(idleMinutes, 1, 60));
        _containerCli = ResolveContainerCli(_configuration["DemoOrchestrator:ContainerCli"]);

        var configuredNetwork = _configuration["DemoOrchestrator:SharedNetworkName"]?.Trim();
        _sharedDemoNetwork = !string.IsNullOrWhiteSpace(configuredNetwork) && ValidSlugRegex().IsMatch(configuredNetwork.Replace('_', '-').ToLowerInvariant())
            ? configuredNetwork
            : DefaultSharedDemoNetwork;

        _memoryLimit = string.IsNullOrWhiteSpace(_configuration["DemoOrchestrator:MemoryLimit"])
            ? "512m"
            : _configuration["DemoOrchestrator:MemoryLimit"]!.Trim();

        _cpuLimit = string.IsNullOrWhiteSpace(_configuration["DemoOrchestrator:CpuLimit"])
            ? "1.0"
            : _configuration["DemoOrchestrator:CpuLimit"]!.Trim();

        var startupSec = _configuration.GetValue<int?>("DemoOrchestrator:StartupTimeoutSeconds") ?? 45;
        _startupTimeout = TimeSpan.FromSeconds(Math.Clamp(startupSec, 15, 120));

        _logger.LogInformation("[DockerOrchestrator] Başlatıldı. CLI={Cli}, Network={Net}, Mem={Mem}, Cpu={Cpu}, IdleTimeout={Minutes}dk",
            _containerCli, _sharedDemoNetwork, _memoryLimit, _cpuLimit, IdleTimeout.TotalMinutes);
    }

    public ActiveDemoSnapshot? GetCurrentActiveSession() => _activeSession;

    public ActiveDemoSnapshot? GetActiveSessionBySlug(string slug)
    {
        var current = _activeSession;
        if (current != null && string.Equals(current.Slug, slug, StringComparison.OrdinalIgnoreCase))
        {
            return current;
        }
        return null;
    }

    public void TouchActivity(string slug)
    {
        var current = _activeSession;
        if (current == null || !string.Equals(current.Slug, slug, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var now = DateTime.UtcNow;
        current.LastActivityUtc = now;

        // Veritabanı I/O'sunu yormamak için en fazla 30 saniyede bir DB'ye yaz
        if (now - _lastDbActivitySyncUtc >= TimeSpan.FromSeconds(30))
        {
            _lastDbActivitySyncUtc = now;
            var sessionId = current.SessionId;
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    await db.ActiveDemoSessions
                        .Where(s => s.Id == sessionId && s.IsActive)
                        .ExecuteUpdateAsync(s => s.SetProperty(x => x.LastActivityUtc, now));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[DockerOrchestrator] LastActivityUtc veritabanı senkronizasyonu başarısız.");
                }
            });
        }
    }

    public async Task<DemoLoadImageResult> LoadImageFromTarAsync(Stream tarStream, string originalFileName, CancellationToken ct = default)
    {
        var tempDir = Path.Combine(_env.ContentRootPath, "Storage", "temp-images");
        Directory.CreateDirectory(tempDir);

        // Kullanıcı dosya adı asla diskte veya komutta kullanılmaz (Path Traversal & Command Injection koruması)
        var tempFilePath = Path.Combine(tempDir, $"{Guid.NewGuid():N}.tar");

        try
        {
            await using (var fs = new FileStream(tempFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                await tarStream.CopyToAsync(fs, ct);
            }

            _logger.LogInformation("[DockerOrchestrator] .tar imaj arşivi yüklendi, engine'e aktarılıyor. TempPath={Path}", tempFilePath);

            var (exitCode, stdout, stderr) = await RunCliAsync(new[] { "load", "-i", tempFilePath }, TimeSpan.FromMinutes(5), ct);

            if (exitCode != 0)
            {
                _logger.LogError("[DockerOrchestrator] Container imajı yüklenemedi. ExitCode={Code}, Stderr={Err}", exitCode, stderr);
                return new DemoLoadImageResult
                {
                    Success = false,
                    Message = $"İmaj yüklenirken hata oluştu: {(string.IsNullOrWhiteSpace(stderr) ? stdout : stderr).Trim()}"
                };
            }

            var parsedImageName = ParseLoadedImageName(stdout, stderr);
            _logger.LogInformation("[DockerOrchestrator] İmaj başarıyla yüklendi. ParsedImage={Image}", parsedImageName);

            return new DemoLoadImageResult
            {
                Success = true,
                LoadedImageName = parsedImageName,
                Message = string.IsNullOrWhiteSpace(parsedImageName)
                    ? "İmaj başarıyla Docker Engine'e yüklendi. İmaj adını manuel doğrulayınız."
                    : $"İmaj başarıyla yüklendi: {parsedImageName}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DockerOrchestrator] .tar yükleme sırasında beklenmeyen hata.");
            return new DemoLoadImageResult
            {
                Success = false,
                Message = $"İmaj yüklenirken sistem hatası oluştu: {ex.Message}"
            };
        }
        finally
        {
            // Eski laptop disk alanını korumak için geçici .tar dosyasını kesinlikle sil
            try
            {
                if (File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[DockerOrchestrator] Geçici tar dosyası silinemedi: {Path}", tempFilePath);
            }
        }
    }

    public async Task<DemoStartResult> StartDemoAsync(int projectId, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var project = await db.Projects
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == projectId && p.IsActive, ct);

            if (project == null || !project.IsDemoEnabled || string.IsNullOrWhiteSpace(project.DockerImageName))
            {
                return new DemoStartResult
                {
                    Success = false,
                    Message = "Bu proje için isteğe bağlı canlı demo yapılandırılmamış."
                };
            }

            var slug = NormalizeSlug(string.IsNullOrWhiteSpace(project.Slug) ? project.Title : project.Slug, project.Id);
            var imageName = project.DockerImageName.Trim();

            if (!ValidImageNameRegex().IsMatch(imageName))
            {
                _logger.LogWarning("[DockerOrchestrator] Geçersiz Docker imaj adı reddedildi. ProjectId={Id}, Image={Image}", projectId, imageName);
                return new DemoStartResult
                {
                    Success = false,
                    Message = "Geçersiz container imaj adı formatı."
                };
            }

            // 1. Halihazırda aktif bir demo var mı kontrol et (Concurrency Lock: Max 1 Aktif Demo)
            var existing = _activeSession;
            if (existing != null)
            {
                var isRunning = await IsContainerRunningAsync(existing.ContainerId, ct);
                if (!isRunning)
                {
                    // Container dışarıdan çökmüş veya kapanmışsa temizle ve devam et
                    await MarkSessionStoppedInDbAsync(db, existing.SessionId, "ContainerExited", ct);
                    _activeSession = null;
                }
                else if (existing.ProjectId == project.Id)
                {
                    // Aynı projenin demosu zaten ayakta -> aktiviteyi güncelle ve direkt döndür
                    existing.LastActivityUtc = DateTime.UtcNow;
                    var remaining = Math.Max(1, (int)(IdleTimeout - (DateTime.UtcNow - existing.LastActivityUtc)).TotalSeconds);
                    return new DemoStartResult
                    {
                        Success = true,
                        DemoUrl = $"/live-demo/{existing.Slug}/",
                        Slug = existing.Slug,
                        RemainingSeconds = remaining,
                        Message = "Canlı demo zaten hazır ve çalışıyor."
                    };
                }
                else
                {
                    // Başka bir proje demosu aktif -> kalan süreyi hesaplayıp şık bekleme mesajı dön
                    var elapsed = DateTime.UtcNow - existing.LastActivityUtc;
                    var remainingSec = Math.Max(15, (int)(IdleTimeout - elapsed).TotalSeconds);
                    var remMin = remainingSec / 60;
                    var remSecPart = remainingSec % 60;

                    return new DemoStartResult
                    {
                        Success = false,
                        IsBusy = true,
                        ActiveProjectTitle = existing.ProjectTitle,
                        RemainingSeconds = remainingSec,
                        Message = $"Donanım koruması gereği aynı anda yalnızca 1 canlı demo çalışabilir. Şu an '{existing.ProjectTitle}' demosu aktif. Tahmini otomatik kapanma süresi: {remMin} dk {remSecPart} sn."
                    };
                }
            }

            // 2. Dinamik yerel port bul (5100-5199)
            var hostPort = FindAvailablePort(MinHostPort, MaxHostPort);
            if (hostPort == null)
            {
                return new DemoStartResult
                {
                    Success = false,
                    Message = "Uygun yerel demo portu (5100-5199) bulunamadı."
                };
            }

            var internalPort = project.ContainerInternalPort is >= 1 and <= 65535 ? project.ContainerInternalPort : 8080;
            var containerName = $"{ContainerNamePrefix}{slug}-{Guid.NewGuid().ToString("N")[..6]}";

            // 3. Ortak izole Docker ağı (divan_demo_net) kontrolü
            if (project.UseSharedDemoNetwork)
            {
                await EnsureDemoNetworkExistsAsync(ct);
            }

            // 4. Güvenli argüman listesi ile container başlat (Sıfır Command Injection)
            var args = new List<string>
            {
                "run", "-d", "--rm",
                "--name", containerName,
                $"--memory={_memoryLimit}",
                $"--cpus={_cpuLimit}",
                "--read-only",
                "--tmpfs", "/tmp:rw,noexec,nosuid,mode=1777,size=64m",
                "--tmpfs", "/root/.aspnet:rw,mode=1777,size=16m",
                "--tmpfs", "/home:rw,mode=1777,size=16m",
                "--tmpfs", "/app/InternalStorage:rw,noexec,nosuid,mode=1777,size=64m",
                "--tmpfs", "/app/App_Data:rw,noexec,nosuid,mode=1777,size=32m",
                "--tmpfs", "/app/Logs:rw,noexec,nosuid,mode=1777,size=32m",
                "--tmpfs", "/app/Storage:rw,noexec,nosuid,mode=1777,size=64m",
                "-v", "/app/wwwroot",
                "--add-host=host.containers.internal:host-gateway",
                "--security-opt=no-new-privileges:true",
                "-p", $"127.0.0.1:{hostPort.Value}:{internalPort}"
            };

            if (project.UseSharedDemoNetwork)
            {
                args.Add("--network");
                args.Add(_sharedDemoNetwork);
            }

            // Ortam değişkenlerini güvenli ayrıştır
            var envPairs = ParseEnvironmentVariables(project.EnvironmentVariables);
            if (!envPairs.ContainsKey("ASPNETCORE_ENVIRONMENT"))
            {
                envPairs["ASPNETCORE_ENVIRONMENT"] = "Demo";
            }

            foreach (var kv in envPairs)
            {
                args.Add("-e");
                args.Add($"{kv.Key}={kv.Value}");
            }

            args.Add(imageName);

            _logger.LogInformation("[DockerOrchestrator] Demo container başlatılıyor: Name={Name}, Image={Image}, Port=127.0.0.1:{HostPort}->{InternalPort}",
                containerName, imageName, hostPort.Value, internalPort);

            var (exitCode, stdout, stderr) = await RunCliAsync(args, TimeSpan.FromSeconds(45), ct);
            if (exitCode != 0)
            {
                _logger.LogError("[DockerOrchestrator] Container başlatılamadı. ExitCode={Code}, Stderr={Err}", exitCode, stderr);
                return new DemoStartResult
                {
                    Success = false,
                    Message = $"Demo container başlatılamadı: {(string.IsNullOrWhiteSpace(stderr) ? stdout : stderr).Trim()}"
                };
            }

            var containerId = stdout.Trim();
            if (string.IsNullOrWhiteSpace(containerId))
            {
                containerId = containerName;
            }

            // 5. Erken çökme kontrolü: Container'ın ayağa kalkıp kapanmadığından emin olmak için 1.5 saniye bekle ve inspect et
            await Task.Delay(1500, ct);

            var (inspectExitCode, runningStdout, _) = await RunCliAsync(
                new[] { "inspect", "-f", "{{.State.Running}}", containerName },
                TimeSpan.FromSeconds(5),
                ct);

            var isStartedAndRunning = inspectExitCode == 0 && runningStdout.Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
            if (!isStartedAndRunning)
            {
                // Container hemen Exited durumuna düştüyse podman logs ve exit kodunu yakala
                var (_, logStdout, logStderr) = await RunCliAsync(
                    new[] { "logs", "--tail", "40", containerName },
                    TimeSpan.FromSeconds(10),
                    ct);
                var rawLogs = (string.IsNullOrWhiteSpace(logStderr) ? logStdout : $"{logStdout}\n{logStderr}").Trim();

                var (_, exitCodeOut, _) = await RunCliAsync(
                    new[] { "inspect", "-f", "{{.State.ExitCode}}", containerName },
                    TimeSpan.FromSeconds(5),
                    ct);
                var exitCodeStr = exitCodeOut.Trim();

                _logger.LogError("[DockerOrchestrator] Container başlatıldıktan hemen sonra çöktü! Container={Name}, ExitCode={ExitCode}, Logs={Logs}",
                    containerName, exitCodeStr, rawLogs);

                await StopAndRemoveContainerAsync(containerId, containerName, ct);

                var displayLogs = string.IsNullOrWhiteSpace(rawLogs) ? "Container log kaydı bulunamadı." : rawLogs;
                if (displayLogs.Length > 750)
                {
                    displayLogs = displayLogs[^750..];
                }

                return new DemoStartResult
                {
                    Success = false,
                    Message = $"Container başlatıldıktan hemen sonra çöktü (ExitCode: {(string.IsNullOrEmpty(exitCodeStr) ? "?" : exitCodeStr)}).\nLog Çıktısı:\n{displayLogs}"
                };
            }

            // 6. Container'ın HTTP uygulamasının yanıt vermeye başlamasını bekle
            var portReady = await WaitForPortReadyAsync(hostPort.Value, _startupTimeout, ct);
            if (!portReady)
            {
                var (_, logStdout, logStderr) = await RunCliAsync(
                    new[] { "logs", "--tail", "40", containerName },
                    TimeSpan.FromSeconds(10),
                    ct);
                var rawLogs = (string.IsNullOrWhiteSpace(logStderr) ? logStdout : $"{logStdout}\n{logStderr}").Trim();

                var stillRunning = await IsContainerRunningAsync(containerName, ct);

                _logger.LogError("[DockerOrchestrator] Container portu zamanında dinlemeye başlamadı. Port={Port}, Running={Running}, Logs={Logs}",
                    hostPort.Value, stillRunning, rawLogs);

                await StopAndRemoveContainerAsync(containerId, containerName, ct);

                var displayLogs = string.IsNullOrWhiteSpace(rawLogs) ? "Uygulama portu dinlemedi veya yanıt vermedi." : rawLogs;
                if (displayLogs.Length > 750)
                {
                    displayLogs = displayLogs[^750..];
                }

                return new DemoStartResult
                {
                    Success = false,
                    Message = stillRunning
                        ? $"Demo container çalışıyor ancak {internalPort} portunu dinlemeye başlamadı (Zaman aşımı).\nLog:\n{displayLogs}"
                        : $"Demo container portu dinlemeye başlamadan kapandı.\nLog:\n{displayLogs}"
                };
            }

            // 7. Veritabanına ActiveDemoSession kaydı aç
            var now = DateTime.UtcNow;
            var session = new ActiveDemoSession
            {
                ProjectId = project.Id,
                Slug = slug,
                ContainerId = containerId.Length > 128 ? containerId[..128] : containerId,
                ContainerName = containerName,
                ImageName = imageName,
                HostPort = hostPort.Value,
                ContainerPort = internalPort,
                StartedAtUtc = now,
                LastActivityUtc = now,
                IsActive = true
            };

            db.ActiveDemoSessions.Add(session);
            await db.SaveChangesAsync(ct);

            _activeSession = new ActiveDemoSnapshot
            {
                SessionId = session.Id,
                ProjectId = project.Id,
                ProjectTitle = project.Title,
                Slug = slug,
                ContainerId = session.ContainerId,
                ContainerName = containerName,
                ImageName = imageName,
                HostPort = hostPort.Value,
                ContainerPort = internalPort,
                StartedAtUtc = now,
                LastActivityUtc = now
            };
            _lastDbActivitySyncUtc = now;

            _logger.LogInformation("[DockerOrchestrator] Demo container hazır! Slug={Slug}, Port={Port}", slug, hostPort.Value);

            return new DemoStartResult
            {
                Success = true,
                DemoUrl = $"/live-demo/{slug}/",
                Slug = slug,
                RemainingSeconds = (int)IdleTimeout.TotalSeconds,
                Message = "Canlı demo ortamı başarıyla hazırlandı."
            };
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> StopActiveDemoAsync(string reason = "ManualStop", CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var current = _activeSession;
            if (current == null)
            {
                return false;
            }

            _logger.LogInformation("[DockerOrchestrator] Aktif demo durduruluyor. Name={Name}, Reason={Reason}", current.ContainerName, reason);

            await StopAndRemoveContainerAsync(current.ContainerId, current.ContainerName, ct);

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await MarkSessionStoppedInDbAsync(db, current.SessionId, reason, ct);

            _activeSession = null;
            return true;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task CleanupIdleDemoAsync(CancellationToken ct = default)
    {
        var current = _activeSession;
        if (current == null)
        {
            return;
        }

        var idleDuration = DateTime.UtcNow - current.LastActivityUtc;
        if (idleDuration >= IdleTimeout)
        {
            _logger.LogInformation("[DockerOrchestrator] Demo {Minutes:F1} dakikadır hareketsiz. Otomatik uyku moduna alınıyor. Container={Name}",
                idleDuration.TotalMinutes, current.ContainerName);
            await StopActiveDemoAsync("IdleTimeout", ct);
        }
    }

    public async Task CleanupOrphanedContainersAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            // Önceki çalışmadan kalmış divan-demo-* container'ları temizle
            var (exitCode, stdout, _) = await RunCliAsync(
                new[] { "ps", "-a", "--filter", $"name={ContainerNamePrefix}", "--format", "{{.ID}}" },
                TimeSpan.FromSeconds(15),
                ct);

            if (exitCode == 0 && !string.IsNullOrWhiteSpace(stdout))
            {
                var ids = stdout.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (var id in ids)
                {
                    if (!string.IsNullOrWhiteSpace(id))
                    {
                        _logger.LogWarning("[DockerOrchestrator] Yetim demo container temizleniyor: {Id}", id);
                        await RunCliAsync(new[] { "rm", "-f", id }, TimeSpan.FromSeconds(15), ct);
                    }
                }
            }

            // DB'de açık kalmış eski oturumları kapat
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var now = DateTime.UtcNow;
            await db.ActiveDemoSessions
                .Where(s => s.IsActive)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.IsActive, false)
                    .SetProperty(x => x.StoppedAtUtc, now)
                    .SetProperty(x => x.StoppedReason, "StartupCleanup"), ct);

            _activeSession = null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[DockerOrchestrator] Başlangıç temizliği sırasında uyarı oluştu (Docker/Podman servisi henüz hazır olmayabilir).");
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task StopAndRemoveContainerAsync(string containerId, string containerName, CancellationToken ct)
    {
        var target = !string.IsNullOrWhiteSpace(containerId) ? containerId : containerName;
        await RunCliAsync(new[] { "stop", "-t", "5", target }, TimeSpan.FromSeconds(20), ct);
        await RunCliAsync(new[] { "rm", "-f", target }, TimeSpan.FromSeconds(15), ct);
    }

    private async Task<bool> IsContainerRunningAsync(string containerId, CancellationToken ct)
    {
        var (exitCode, stdout, _) = await RunCliAsync(
            new[] { "inspect", "-f", "{{.State.Running}}", containerId },
            TimeSpan.FromSeconds(10),
            ct);

        return exitCode == 0 && stdout.Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    private async Task EnsureDemoNetworkExistsAsync(CancellationToken ct)
    {
        var (inspectCode, _, _) = await RunCliAsync(new[] { "network", "inspect", _sharedDemoNetwork }, TimeSpan.FromSeconds(10), ct);
        if (inspectCode != 0)
        {
            _logger.LogInformation("[DockerOrchestrator] İzole demo ağı oluşturuluyor: {Network}", _sharedDemoNetwork);
            await RunCliAsync(new[] { "network", "create", _sharedDemoNetwork }, TimeSpan.FromSeconds(15), ct);
        }
    }

    private static async Task MarkSessionStoppedInDbAsync(ApplicationDbContext db, int sessionId, string reason, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        await db.ActiveDemoSessions
            .Where(s => s.Id == sessionId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.IsActive, false)
                .SetProperty(x => x.StoppedAtUtc, now)
                .SetProperty(x => x.StoppedReason, reason), ct);
    }

    private static async Task<bool> WaitForPortReadyAsync(int port, TimeSpan timeout, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + timeout;
        using var handler = new SocketsHttpHandler
        {
            ConnectTimeout = TimeSpan.FromMilliseconds(800),
            AllowAutoRedirect = false
        };
        using var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(3)
        };

        while (DateTime.UtcNow < deadline && !ct.IsCancellationRequested)
        {
            try
            {
                // Rootless Podman ortamında host portu hemen açık gözükse bile, container
                // içindeki Kestrel (EF Core, DbSeeder vb.) hazır olana kadar TCP RST (Connection Reset) döner.
                // Bu yüzden gerçek bir HTTP isteği gönderip Kestrel'den herhangi bir HTTP yanıtı (2xx, 3xx, 4xx) bekliyoruz.
                using var response = await client.GetAsync($"http://127.0.0.1:{port}/", HttpCompletionOption.ResponseHeadersRead, ct);
                
                // HTTP pipeline hazır! Uygulamanın kararlı duruma geçmesi için kısa bir bekleme
                await Task.Delay(400, ct);
                return true;
            }
            catch
            {
                // Henüz Kestrel başlamadı, Connection Reset aldı veya zaman aşımı; bekle ve tekrar dene
                await Task.Delay(500, ct);
            }
        }
        return false;
    }

    private static int? FindAvailablePort(int minPort, int maxPort)
    {
        for (var port = minPort; port <= maxPort; port++)
        {
            try
            {
                using var listener = new TcpListener(IPAddress.Loopback, port);
                listener.Start();
                listener.Stop();
                return port;
            }
            catch (SocketException)
            {
                // Port kullanımda, sonrakini dene
            }
        }
        return null;
    }

    private static Dictionary<string, string> ParseEnvironmentVariables(string? rawEnv)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(rawEnv))
        {
            return result;
        }

        var lines = rawEnv.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith('#'))
                continue;

            var eqIndex = line.IndexOf('=');
            if (eqIndex <= 0)
                continue;

            var key = line[..eqIndex].Trim();
            var value = line[(eqIndex + 1)..].Trim();

            if (ValidEnvKeyRegex().IsMatch(key))
            {
                result[key] = value;
            }
        }

        return result;
    }

    private static string? ParseLoadedImageName(string? stdout, string? stderr = null)
    {
        var combined = $"{stdout}\n{stderr}";
        if (string.IsNullOrWhiteSpace(combined))
            return null;

        // ANSI escape kodlarını temizle (terminallerde renklendirme çıktısı gelebilir)
        var clean = Regex.Replace(combined, @"\x1B\[[^@-~]*[@-~]", "");
        var lines = clean.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        // Genelde son satırlarda "Loaded image" bilgisi yer alır, tersten tarayalım
        for (var i = lines.Length - 1; i >= 0; i--)
        {
            var line = lines[i];

            // 1. Regex ile yakalama: "Loaded image: localhost/imaj:tag" veya "Loaded image(s): localhost/imaj:tag"
            var match = Regex.Match(line, @"Loaded\s+image(?:\(s\))?:\s*(.+)$", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var candidate = match.Groups[1].Value.Trim().Trim('\'', '"');
                if (candidate.Contains(','))
                {
                    candidate = candidate.Split(',')[0].Trim().Trim('\'', '"');
                }

                if (!string.IsNullOrWhiteSpace(candidate) && ValidImageNameRegex().IsMatch(candidate))
                {
                    return candidate;
                }
            }

            // 2. Yedek: idx bazlı yakalama
            var idx = line.IndexOf(':');
            if (idx > 0 && line[..idx].Contains("Loaded image", StringComparison.OrdinalIgnoreCase))
            {
                var candidate = line[(idx + 1)..].Trim().Trim('\'', '"');
                if (candidate.Contains(','))
                {
                    candidate = candidate.Split(',')[0].Trim().Trim('\'', '"');
                }

                if (!string.IsNullOrWhiteSpace(candidate) && ValidImageNameRegex().IsMatch(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    public static string NormalizeSlug(string raw, int fallbackId)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return $"proje-{fallbackId}";

        var lower = raw.Trim().ToLowerInvariant()
            .Replace("ı", "i").Replace("İ", "i")
            .Replace("ğ", "g").Replace("ü", "u")
            .Replace("ş", "s").Replace("ö", "o").Replace("ç", "c");

        var chars = lower.Select(c => char.IsAsciiLetterOrDigit(c) ? c : '-').ToArray();
        var collapsed = Regex.Replace(new string(chars), "-{2,}", "-").Trim('-');

        if (collapsed.Length > 80)
            collapsed = collapsed[..80].Trim('-');

        return ValidSlugRegex().IsMatch(collapsed) ? collapsed : $"proje-{fallbackId}";
    }

    private static string ResolveContainerCli(string? configured)
    {
        if (!string.IsNullOrWhiteSpace(configured) && !configured.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            var trimmed = configured.Trim();
            // systemd servis ortamlarında PATH kısıtlı olabileceği için standart sistem dizinlerini doğrudan çözümle
            if (!trimmed.Contains('/') && !trimmed.Contains('\\'))
            {
                if (File.Exists($"/usr/bin/{trimmed}")) return $"/usr/bin/{trimmed}";
                if (File.Exists($"/usr/local/bin/{trimmed}")) return $"/usr/local/bin/{trimmed}";
            }
            return trimmed;
        }

        // Debian 13 + Rootless Podman öncelikli tam sistem yolu çözümlemesi (sabit geliştirici dizini içermez)
        if (File.Exists("/usr/bin/podman")) return "/usr/bin/podman";
        if (File.Exists("/usr/local/bin/podman")) return "/usr/local/bin/podman";
        if (File.Exists("/usr/bin/docker")) return "/usr/bin/docker";
        if (File.Exists("/usr/local/bin/docker")) return "/usr/local/bin/docker";

        return "podman";
    }

    private async Task<(int ExitCode, string Stdout, string Stderr)> RunCliAsync(
        IEnumerable<string> arguments,
        TimeSpan timeout,
        CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _containerCli,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        // Command Injection'a tamamen kapalı güvenli argüman ekleme
        foreach (var arg in arguments)
        {
            psi.ArgumentList.Add(arg);
        }

        using var process = new Process { StartInfo = psi };
        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            return (-1, string.Empty, $"Container CLI ({_containerCli}) çalıştırılamadı: {ex.Message}");
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeout);

        var stdoutTask = process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
        var stderrTask = process.StandardError.ReadToEndAsync(timeoutCts.Token);

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
            return (process.ExitCode, await stdoutTask, await stderrTask);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { /* ignore */ }
            return (-1, string.Empty, "Container komutu zaman aşımına uğradı.");
        }
    }
}
