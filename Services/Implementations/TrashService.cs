using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Nights.Data;
using Nights.Services.Interfaces;

namespace Nights.Services.Implementations;

public sealed class TrashService : ITrashService
{
    private readonly ApplicationDbContext _context;
    private readonly IImageService _imageService;
    private readonly IMemoryCache _cache;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<TrashService> _logger;

    public int RetentionDays { get; }
    public int AuditLogRetentionDays { get; }

    public TrashService(
        ApplicationDbContext context,
        IImageService imageService,
        IMemoryCache cache,
        IAuditLogService auditLogService,
        IConfiguration configuration,
        ILogger<TrashService> logger)
    {
        _context = context;
        _imageService = imageService;
        _cache = cache;
        _auditLogService = auditLogService;
        _logger = logger;

        RetentionDays = Math.Clamp(configuration.GetValue<int?>("Maintenance:TrashRetentionDays") ?? 30, 1, 365);
        AuditLogRetentionDays = Math.Clamp(configuration.GetValue<int?>("Maintenance:AuditLogRetentionDays") ?? 60, 7, 365);
    }

    public async Task<TrashBinOverviewDto> GetTrashOverviewAsync(string? entityTypeFilter = null, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var items = new List<TrashItemDto>();

        var memories = await _context.Memories
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(m => m.IsDeleted)
            .OrderByDescending(m => m.DeletedAtUtc ?? m.CreatedAt)
            .ToListAsync(ct);

        var projects = await _context.Projects
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(p => p.IsDeleted)
            .OrderByDescending(p => p.DeletedAtUtc ?? p.CreatedAt)
            .ToListAsync(ct);

        var notes = await _context.Notes
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(n => n.IsDeleted)
            .OrderByDescending(n => n.DeletedAtUtc ?? n.UpdatedAt ?? n.CreatedAt)
            .ToListAsync(ct);

        bool Include(string type) =>
            string.IsNullOrWhiteSpace(entityTypeFilter) ||
            entityTypeFilter.Equals("All", StringComparison.OrdinalIgnoreCase) ||
            entityTypeFilter.Equals(type, StringComparison.OrdinalIgnoreCase);

        if (Include("Memory"))
        {
            foreach (var m in memories)
            {
                var deletedAt = m.DeletedAtUtc ?? m.CreatedAt;
                var remaining = Math.Max(0, RetentionDays - (int)Math.Floor((now - deletedAt).TotalDays));
                items.Add(new TrashItemDto
                {
                    Id = m.Id,
                    EntityType = "Memory",
                    EntityTypeDisplay = "Anı",
                    Title = m.Description.Length > 70 ? m.Description[..70] + "..." : m.Description,
                    Subtitle = m.IsApproved ? "Onaylı Anı (uploads/trash)" : "Bekleyen Anı (uploads/trash)",
                    PreviewImageFileName = m.ImageFileName,
                    DeletedAtUtc = deletedAt,
                    DeletedBy = m.DeletedBy ?? "Admin",
                    DaysRemaining = remaining
                });
            }
        }

        if (Include("Project"))
        {
            foreach (var p in projects)
            {
                var deletedAt = p.DeletedAtUtc ?? p.CreatedAt;
                var remaining = Math.Max(0, RetentionDays - (int)Math.Floor((now - deletedAt).TotalDays));
                items.Add(new TrashItemDto
                {
                    Id = p.Id,
                    EntityType = "Project",
                    EntityTypeDisplay = "Proje",
                    Title = p.Title,
                    Subtitle = string.IsNullOrWhiteSpace(p.DockerImageName) ? p.TechStack : $"Docker: {p.DockerImageName}",
                    DeletedAtUtc = deletedAt,
                    DeletedBy = p.DeletedBy ?? "Admin",
                    DaysRemaining = remaining
                });
            }
        }

        if (Include("Note"))
        {
            foreach (var n in notes)
            {
                var deletedAt = n.DeletedAtUtc ?? n.UpdatedAt ?? n.CreatedAt;
                var remaining = Math.Max(0, RetentionDays - (int)Math.Floor((now - deletedAt).TotalDays));
                items.Add(new TrashItemDto
                {
                    Id = n.Id,
                    EntityType = "Note",
                    EntityTypeDisplay = "Varak (Not)",
                    Title = n.Title,
                    Subtitle = $"Yazar: {n.AuthorName} • {n.WordCount} kelime",
                    DeletedAtUtc = deletedAt,
                    DeletedBy = n.DeletedBy ?? n.AuthorName,
                    DaysRemaining = remaining
                });
            }
        }

        return new TrashBinOverviewDto
        {
            RetentionDays = RetentionDays,
            MemoryCount = memories.Count,
            ProjectCount = projects.Count,
            NoteCount = notes.Count,
            Items = items.OrderByDescending(i => i.DeletedAtUtc).ToList()
        };
    }

    public async Task<(bool Success, string Message)> RestoreAsync(string entityType, int id, string performedBy, CancellationToken ct = default)
    {
        if (entityType.Equals("Memory", StringComparison.OrdinalIgnoreCase))
        {
            var memory = await _context.Memories
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(m => m.Id == id && m.IsDeleted, ct);

            if (memory == null) return (false, "Çöp kutusunda ilgili anı bulunamadı.");

            // Fiziksel görseli uploads/trash/ dizininden uploads/approved/ (veya pending/) dizinine geri taşı
            _imageService.RestoreFromTrash(memory.ImageFileName, restoreToApproved: memory.IsApproved);

            memory.IsDeleted = false;
            memory.DeletedAtUtc = null;
            memory.DeletedBy = null;
            await _context.SaveChangesAsync(ct);

            _cache.Remove("cache_approved_memories");

            await _auditLogService.LogAsync(
                category: "ItemRestored",
                summary: $"Anı #{memory.Id} çöp kutusundan geri getirildi ve görseli geri taşındı.",
                targetEntityType: "Memory",
                targetEntityId: memory.Id.ToString(),
                logLevel: "Information",
                username: performedBy,
                component: "TrashService",
                ct: ct);

            return (true, "Anı ve görsel dosyası başarıyla geri getirildi.");
        }

        if (entityType.Equals("Project", StringComparison.OrdinalIgnoreCase))
        {
            var project = await _context.Projects
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.Id == id && p.IsDeleted, ct);

            if (project == null) return (false, "Çöp kutusunda ilgili proje bulunamadı.");

            project.IsDeleted = false;
            project.DeletedAtUtc = null;
            project.DeletedBy = null;
            await _context.SaveChangesAsync(ct);

            InvalidateProjectCaches(project.Id);

            await _auditLogService.LogAsync(
                category: "ItemRestored",
                summary: $"'{project.Title}' projesi (#{project.Id}) çöp kutusundan geri getirildi.",
                targetEntityType: "Project",
                targetEntityId: project.Id.ToString(),
                logLevel: "Information",
                username: performedBy,
                component: "TrashService",
                ct: ct);

            return (true, $"'{project.Title}' projesi başarıyla geri getirildi.");
        }

        if (entityType.Equals("Note", StringComparison.OrdinalIgnoreCase))
        {
            var note = await _context.Notes
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(n => n.Id == id && n.IsDeleted, ct);

            if (note == null) return (false, "Çöp kutusunda ilgili not bulunamadı.");

            note.IsDeleted = false;
            note.DeletedAtUtc = null;
            note.DeletedBy = null;
            note.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);

            BumpNotesCacheVersion();

            await _auditLogService.LogAsync(
                category: "ItemRestored",
                summary: $"'{note.Title}' notu (#{note.Id}) çöp kutusundan geri getirildi.",
                targetEntityType: "Note",
                targetEntityId: note.Id.ToString(),
                logLevel: "Information",
                username: performedBy,
                component: "TrashService",
                ct: ct);

            return (true, $"'{note.Title}' notu başarıyla geri getirildi.");
        }

        return (false, "Geçersiz varlık türü.");
    }

    public async Task<(bool Success, string Message)> HardDeleteAsync(string entityType, int id, string performedBy, CancellationToken ct = default)
    {
        if (entityType.Equals("Memory", StringComparison.OrdinalIgnoreCase))
        {
            var memory = await _context.Memories
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(m => m.Id == id && m.IsDeleted, ct);

            if (memory == null) return (false, "Çöp kutusunda ilgili anı bulunamadı.");

            _imageService.DeleteFromTrash(memory.ImageFileName);
            _context.Memories.Remove(memory);
            await _context.SaveChangesAsync(ct);

            await _auditLogService.LogAsync(
                category: "ItemHardDeleted",
                summary: $"Anı #{memory.Id} ve fiziksel görseli ({memory.ImageFileName}) kalıcı olarak silindi.",
                targetEntityType: "Memory",
                targetEntityId: memory.Id.ToString(),
                logLevel: "Warning",
                username: performedBy,
                component: "TrashService",
                ct: ct);

            return (true, "Anı ve görsel dosyası diskten kalıcı olarak silindi.");
        }

        if (entityType.Equals("Project", StringComparison.OrdinalIgnoreCase))
        {
            var project = await _context.Projects
                .IgnoreQueryFilters()
                .Include(p => p.Tutorials)
                .Include(p => p.DemoSessions)
                .FirstOrDefaultAsync(p => p.Id == id && p.IsDeleted, ct);

            if (project == null) return (false, "Çöp kutusunda ilgili proje bulunamadı.");

            var title = project.Title;
            _context.Projects.Remove(project);
            await _context.SaveChangesAsync(ct);

            InvalidateProjectCaches(id);

            await _auditLogService.LogAsync(
                category: "ItemHardDeleted",
                summary: $"'{title}' projesi (#{id}) veritabanından kalıcı olarak silindi.",
                targetEntityType: "Project",
                targetEntityId: id.ToString(),
                logLevel: "Warning",
                username: performedBy,
                component: "TrashService",
                ct: ct);

            return (true, $"'{title}' projesi kalıcı olarak silindi.");
        }

        if (entityType.Equals("Note", StringComparison.OrdinalIgnoreCase))
        {
            var note = await _context.Notes
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(n => n.Id == id && n.IsDeleted, ct);

            if (note == null) return (false, "Çöp kutusunda ilgili not bulunamadı.");

            var title = note.Title;
            _context.Notes.Remove(note);
            await _context.SaveChangesAsync(ct);

            BumpNotesCacheVersion();

            await _auditLogService.LogAsync(
                category: "ItemHardDeleted",
                summary: $"'{title}' notu (#{id}) veritabanından kalıcı olarak silindi.",
                targetEntityType: "Note",
                targetEntityId: id.ToString(),
                logLevel: "Warning",
                username: performedBy,
                component: "TrashService",
                ct: ct);

            return (true, $"'{title}' notu kalıcı olarak silindi.");
        }

        return (false, "Geçersiz varlık türü.");
    }

    public async Task<TrashPurgeSummaryDto> PurgeExpiredAsync(CancellationToken ct = default)
    {
        var thresholdUtc = DateTime.UtcNow.AddDays(-RetentionDays);

        var expiredMemories = await _context.Memories
            .IgnoreQueryFilters()
            .Where(m => m.IsDeleted && (m.DeletedAtUtc ?? m.CreatedAt) <= thresholdUtc)
            .ToListAsync(ct);

        var expiredProjects = await _context.Projects
            .IgnoreQueryFilters()
            .Where(p => p.IsDeleted && (p.DeletedAtUtc ?? p.CreatedAt) <= thresholdUtc)
            .ToListAsync(ct);

        var expiredNotes = await _context.Notes
            .IgnoreQueryFilters()
            .Where(n => n.IsDeleted && (n.DeletedAtUtc ?? n.UpdatedAt ?? n.CreatedAt) <= thresholdUtc)
            .ToListAsync(ct);

        var filesDeleted = 0;
        foreach (var mem in expiredMemories)
        {
            if (!string.IsNullOrWhiteSpace(mem.ImageFileName))
            {
                _imageService.DeleteFromTrash(mem.ImageFileName);
                filesDeleted++;
            }
        }

        if (expiredMemories.Count > 0) _context.Memories.RemoveRange(expiredMemories);
        if (expiredProjects.Count > 0) _context.Projects.RemoveRange(expiredProjects);
        if (expiredNotes.Count > 0) _context.Notes.RemoveRange(expiredNotes);

        if (expiredMemories.Count > 0 || expiredProjects.Count > 0 || expiredNotes.Count > 0)
        {
            await _context.SaveChangesAsync(ct);
        }

        await _context.GameScores
            .IgnoreQueryFilters()
            .Where(g => g.IsDeleted && (g.DeletedAtUtc ?? g.AchievedAtUtc) <= thresholdUtc)
            .ExecuteDeleteAsync(ct);

        var prunedLogs = await _auditLogService.PurgeOlderThanDaysAsync(AuditLogRetentionDays, ct);

        var summary = new TrashPurgeSummaryDto
        {
            MemoriesPurged = expiredMemories.Count,
            ProjectsPurged = expiredProjects.Count,
            NotesPurged = expiredNotes.Count,
            FilesDeleted = filesDeleted,
            AuditLogsPruned = prunedLogs
        };

        if (summary.TotalEntitiesPurged > 0 || summary.AuditLogsPruned > 0)
        {
            _logger.LogInformation(
                "[TrashService] Otomatik temizlik tamamlandı. Anı={Mem}, Proje={Proj}, Not={Note}, Dosya={Files}, Log={Logs}",
                summary.MemoriesPurged, summary.ProjectsPurged, summary.NotesPurged, summary.FilesDeleted, summary.AuditLogsPruned);

            await _auditLogService.LogAsync(
                category: "MaintenanceExecuted",
                summary: $"Otomatik periyodik temizlik tamamlandı: {summary.TotalEntitiesPurged} süresi dolmuş kayıt (Anı: {summary.MemoriesPurged}, Proje: {summary.ProjectsPurged}, Not: {summary.NotesPurged}), {summary.FilesDeleted} dosya ve {summary.AuditLogsPruned} eski log temizlendi.",
                logLevel: "Information",
                username: "DivanTrashPurgeWorker",
                ipAddress: "127.0.0.1",
                component: "DivanTrashPurgeWorker",
                ct: ct);
        }

        return summary;
    }

    private void InvalidateProjectCaches(int projectId)
    {
        _cache.Remove("cache_active_projects");
        _cache.Remove("cache_featured_projects_3");
        _cache.Remove($"cache_project_detail_{projectId}");
    }

    private void BumpNotesCacheVersion()
    {
        const string key = "cache_notes_version";
        var current = _cache.TryGetValue(key, out int ver) ? ver : 1;
        _cache.Set(key, current + 1);
    }
}
