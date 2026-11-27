using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Nights.Data;
using Nights.Services.Interfaces;

namespace Nights.Services.Implementations;

public sealed class MaintenanceService : IMaintenanceService
{
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _env;
    private readonly IMemoryCache _cache;
    private readonly IDockerOrchestratorService _orchestrator;
    private readonly IAuditLogService _auditLogService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MaintenanceService> _logger;

    private readonly string _uploadsBase;
    private readonly string _tempImagesPath;
    private readonly string _cliPath;
    private readonly string _sharedNetworkName;

    public MaintenanceService(
        ApplicationDbContext context,
        IWebHostEnvironment env,
        IMemoryCache cache,
        IDockerOrchestratorService orchestrator,
        IAuditLogService auditLogService,
        IConfiguration configuration,
        ILogger<MaintenanceService> logger)
    {
        _context = context;
        _env = env;
        _cache = cache;
        _orchestrator = orchestrator;
        _auditLogService = auditLogService;
        _configuration = configuration;
        _logger = logger;

        _uploadsBase = Path.Combine(_env.ContentRootPath, "Storage", "uploads");
        _tempImagesPath = Path.Combine(_env.ContentRootPath, "Storage", "temp-images");
        _cliPath = ResolveCli(configuration["DemoOrchestrator:ContainerCli"]);
        _sharedNetworkName = string.IsNullOrWhiteSpace(configuration["DemoOrchestrator:SharedNetworkName"])
            ? "divan_demo_net"
            : configuration["DemoOrchestrator:SharedNetworkName"]!.Trim();
    }

    public async Task<MaintenanceDiagnosticsDto> GetDiagnosticsAsync(CancellationToken ct = default)
    {
        var orphanFiles = await ScanOrphanFilesAsync(ct);
        var podmanHealth = await CheckPodmanHealthAsync(ct);
        var dbStats = await GetDatabaseStatsAsync(ct);

        return new MaintenanceDiagnosticsDto
        {
            OrphanFiles = orphanFiles,
            PodmanHealth = podmanHealth,
            DatabaseStats = dbStats,
            GeneratedAtUtc = DateTime.UtcNow
        };
    }

    public async Task<(int DeletedCount, long FreedBytes)> CleanOrphanFilesAsync(string performedBy, CancellationToken ct = default)
    {
        var orphans = await ScanOrphanFilesAsync(ct);
        var deleted = 0;
        long freedBytes = 0;

        foreach (var item in orphans)
        {
            var fullPath = ResolveOrphanFilePath(item.Folder, item.FileName);
            if (fullPath != null && File.Exists(fullPath))
            {
                try
                {
                    var len = new FileInfo(fullPath).Length;
                    File.Delete(fullPath);
                    deleted++;
                    freedBytes += len;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[MaintenanceService] Yetim dosya silinemedi: {Path}", fullPath);
                }
            }
        }

        await _auditLogService.LogAsync(
            category: "MaintenanceExecuted",
            summary: $"Yetim dosya temizliği çalıştırıldı: {deleted} sahipsiz dosya silindi ({FormatBytes(freedBytes)} alan kazanıldı).",
            targetEntityType: "Storage",
            logLevel: "Information",
            username: performedBy,
            component: "MaintenanceService",
            ct: ct);

        return (deleted, freedBytes);
    }

    public async Task<int> CleanOrphanContainersAsync(string performedBy, CancellationToken ct = default)
    {
        var before = await GetOrphanDemoContainersAsync(ct);
        await _orchestrator.CleanupOrphanedContainersAsync(ct);

        await _auditLogService.LogAsync(
            category: "MaintenanceExecuted",
            summary: $"Yetim Podman/Docker container temizliği çalıştırıldı. Temizlenen yetim container sayısı: {before.Count}.",
            targetEntityType: "Container",
            logLevel: "Information",
            username: performedBy,
            component: "MaintenanceService",
            ct: ct);

        return before.Count;
    }

    public async Task PurgeMemoryCacheAsync(string performedBy, CancellationToken ct = default)
    {
        if (_cache is MemoryCache concreteCache)
        {
            concreteCache.Compact(1.0);
        }

        // Bilinen tüm anahtarları ayrıca temizle
        _cache.Remove("cache_approved_memories");
        _cache.Remove("cache_active_projects");
        _cache.Remove("cache_featured_projects_3");
        _cache.Remove("sys_health_stats_v1");
        _cache.Remove("site_settings_cache");

        // Versiyonlu not önbelleğini sıfırla
        var currentVer = _cache.TryGetValue("cache_notes_version", out int ver) ? ver : 1;
        _cache.Set("cache_notes_version", currentVer + 1);

        await _auditLogService.LogAsync(
            category: "MaintenanceExecuted",
            summary: "Tüm IMemoryCache önbellek anahtarları tek tıkla sıfırlandı (Cache Purge).",
            targetEntityType: "MemoryCache",
            logLevel: "Information",
            username: performedBy,
            component: "MaintenanceService",
            ct: ct);
    }

    private async Task<List<OrphanFileItemDto>> ScanOrphanFilesAsync(CancellationToken ct)
    {
        var result = new List<OrphanFileItemDto>();

        // Çöp kutusundakiler dahil DB'de referansı olan tüm dosya adlarını çek
        var referencedFiles = await _context.Memories
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Select(m => m.ImageFileName)
            .ToListAsync(ct);

        var referencedSet = new HashSet<string>(
            referencedFiles.Where(f => !string.IsNullOrWhiteSpace(f)).Select(Path.GetFileName)!,
            StringComparer.OrdinalIgnoreCase);

        foreach (var folder in new[] { "pending", "approved", "trash" })
        {
            var dir = Path.Combine(_uploadsBase, folder);
            if (!Directory.Exists(dir)) continue;

            foreach (var filePath in Directory.EnumerateFiles(dir))
            {
                var fileName = Path.GetFileName(filePath);
                if (!referencedSet.Contains(fileName))
                {
                    var fi = new FileInfo(filePath);
                    result.Add(new OrphanFileItemDto
                    {
                        Folder = $"uploads/{folder}",
                        FileName = fileName,
                        SizeBytes = fi.Length,
                        LastWriteUtc = fi.LastWriteTimeUtc
                    });
                }
            }
        }

        // Geçici .tar artıkları (temp-images altında 10 dakikadan eski dosyalar)
        if (Directory.Exists(_tempImagesPath))
        {
            var tempCutoff = DateTime.UtcNow.AddMinutes(-10);
            foreach (var filePath in Directory.EnumerateFiles(_tempImagesPath))
            {
                var fi = new FileInfo(filePath);
                if (fi.LastWriteTimeUtc < tempCutoff)
                {
                    result.Add(new OrphanFileItemDto
                    {
                        Folder = "temp-images",
                        FileName = fi.Name,
                        SizeBytes = fi.Length,
                        LastWriteUtc = fi.LastWriteTimeUtc
                    });
                }
            }
        }

        return result.OrderByDescending(f => f.SizeBytes).ToList();
    }

    private string? ResolveOrphanFilePath(string folder, string fileName)
    {
        var safeFile = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(safeFile)) return null;

        return folder switch
        {
            "uploads/pending" => Path.Combine(_uploadsBase, "pending", safeFile),
            "uploads/approved" => Path.Combine(_uploadsBase, "approved", safeFile),
            "uploads/trash" => Path.Combine(_uploadsBase, "trash", safeFile),
            "temp-images" => Path.Combine(_tempImagesPath, safeFile),
            _ => null
        };
    }

    private async Task<PodmanHealthDiagnosticsDto> CheckPodmanHealthAsync(CancellationToken ct)
    {
        var (verCode, verOut, verErr) = await RunCliAsync(new[] { "--version" }, TimeSpan.FromSeconds(5), ct);
        var isAvailable = verCode == 0;
        var versionText = isAvailable ? verOut.Trim() : (string.IsNullOrWhiteSpace(verErr) ? "Erişilemiyor" : verErr.Trim());

        var isNetPresent = false;
        var orphanContainers = new List<string>();
        if (isAvailable)
        {
            var (netCode, _, _) = await RunCliAsync(new[] { "network", "inspect", _sharedNetworkName }, TimeSpan.FromSeconds(5), ct);
            isNetPresent = netCode == 0;
            orphanContainers = await GetOrphanDemoContainersAsync(ct);
        }

        var (diskPercent, diskFree, diskTotal) = GetDiskMetrics();
        var active = _orchestrator.GetCurrentActiveSession();

        return new PodmanHealthDiagnosticsDto
        {
            CliPath = _cliPath,
            IsCliAvailable = isAvailable,
            CliVersion = versionText,
            SharedNetworkName = _sharedNetworkName,
            IsSharedNetworkPresent = isNetPresent,
            OrphanContainers = orphanContainers,
            ActiveDemoName = active?.ContainerName,
            DiskUsagePercent = diskPercent,
            DiskFreeFormatted = diskFree,
            DiskTotalFormatted = diskTotal
        };
    }

    private async Task<List<string>> GetOrphanDemoContainersAsync(CancellationToken ct)
    {
        var (code, stdout, _) = await RunCliAsync(
            new[] { "ps", "-a", "--filter", "name=divan-demo-", "--format", "{{.Names}} ({{.Status}})" },
            TimeSpan.FromSeconds(8),
            ct);

        if (code != 0 || string.IsNullOrWhiteSpace(stdout))
            return new List<string>();

        var activeName = _orchestrator.GetCurrentActiveSession()?.ContainerName;
        var lines = stdout.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return lines
            .Where(line => string.IsNullOrWhiteSpace(activeName) || !line.StartsWith(activeName + " ", StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private async Task<DatabaseStatsDto> GetDatabaseStatsAsync(CancellationToken ct)
    {
        var activeMemories = await _context.Memories.AsNoTracking().CountAsync(ct);
        var activeProjects = await _context.Projects.AsNoTracking().CountAsync(ct);
        var activeNotes = await _context.Notes.AsNoTracking().CountAsync(ct);
        var auditLogs = await _context.SystemAuditLogs.AsNoTracking().CountAsync(ct);
        var demoSessions = await _context.ActiveDemoSessions.IgnoreQueryFilters().AsNoTracking().CountAsync(ct);

        var trashMem = await _context.Memories.IgnoreQueryFilters().AsNoTracking().CountAsync(m => m.IsDeleted, ct);
        var trashProj = await _context.Projects.IgnoreQueryFilters().AsNoTracking().CountAsync(p => p.IsDeleted, ct);
        var trashNote = await _context.Notes.IgnoreQueryFilters().AsNoTracking().CountAsync(n => n.IsDeleted, ct);

        var dbSize = await GetSqlServerDatabaseSizeAsync(ct);

        return new DatabaseStatsDto
        {
            ActiveMemories = activeMemories,
            ActiveProjects = activeProjects,
            ActiveNotes = activeNotes,
            TotalTrashItems = trashMem + trashProj + trashNote,
            TotalAuditLogs = auditLogs,
            TotalDemoSessions = demoSessions,
            DatabaseSizeFormatted = dbSize
        };
    }

    private async Task<string> GetSqlServerDatabaseSizeAsync(CancellationToken ct)
    {
        try
        {
            var conn = _context.Database.GetDbConnection();
            var shouldClose = conn.State != System.Data.ConnectionState.Open;
            if (shouldClose) await conn.OpenAsync(ct);

            try
            {
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT CAST(SUM(size) * 8.0 / 1024.0 AS DECIMAL(10, 2)) FROM sys.database_files";
                var result = await cmd.ExecuteScalarAsync(ct);
                if (result != null && result != DBNull.Value)
                {
                    return $"{result} MB";
                }
            }
            finally
            {
                if (shouldClose) await conn.CloseAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[MaintenanceService] Veritabanı boyutu okunamadı.");
        }
        return "Bilinmiyor";
    }

    private (double percent, string free, string total) GetDiskMetrics()
    {
        try
        {
            var root = Path.GetPathRoot(_env.ContentRootPath) ?? "/";
            var drive = new DriveInfo(root);
            if (drive.IsReady && drive.TotalSize > 0)
            {
                var used = drive.TotalSize - drive.AvailableFreeSpace;
                var pct = Math.Round((double)used / drive.TotalSize * 100.0, 1);
                return (pct, FormatBytes(drive.AvailableFreeSpace), FormatBytes(drive.TotalSize));
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[MaintenanceService] Disk metrikleri okunamadı.");
        }
        return (0, "-", "-");
    }

    private static string ResolveCli(string? configured)
    {
        if (!string.IsNullOrWhiteSpace(configured) && !configured.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            var t = configured.Trim();
            if (!t.Contains('/') && !t.Contains('\\'))
            {
                if (File.Exists($"/usr/bin/{t}")) return $"/usr/bin/{t}";
                if (File.Exists($"/usr/local/bin/{t}")) return $"/usr/local/bin/{t}";
            }
            return t;
        }
        if (File.Exists("/usr/bin/podman")) return "/usr/bin/podman";
        if (File.Exists("/usr/local/bin/podman")) return "/usr/local/bin/podman";
        if (File.Exists("/usr/bin/docker")) return "/usr/bin/docker";
        if (File.Exists("/usr/local/bin/docker")) return "/usr/local/bin/docker";
        return "podman";
    }

    private async Task<(int ExitCode, string Stdout, string Stderr)> RunCliAsync(
        IEnumerable<string> arguments, TimeSpan timeout, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _cliPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var arg in arguments) psi.ArgumentList.Add(arg);

        using var proc = new Process { StartInfo = psi };
        try { proc.Start(); }
        catch (Exception ex) { return (-1, string.Empty, ex.Message); }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);

        var outTask = proc.StandardOutput.ReadToEndAsync(cts.Token);
        var errTask = proc.StandardError.ReadToEndAsync(cts.Token);

        try
        {
            await proc.WaitForExitAsync(cts.Token);
            return (proc.ExitCode, await outTask, await errTask);
        }
        catch (OperationCanceledException)
        {
            try { proc.Kill(entireProcessTree: true); } catch { /* ignore */ }
            return (-1, string.Empty, "Zaman aşımı");
        }
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        var idx = 0;
        while (value >= 1024 && idx < units.Length - 1)
        {
            value /= 1024;
            idx++;
        }
        return $"{value:0.##} {units[idx]}";
    }
}
