using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Nights.Data;
using Nights.Models.Entities;
using Nights.Services.Interfaces;

namespace Nights.Services.Implementations;

/// <summary>
/// Özel Not Defteri Servisi.
/// Performans & Mimari Kuralları:
/// - AsNoTracking() tüm okuma sorgularında uygulanır.
/// - Soft-Delete: Silinen notlar fiziksel olarak kaldırılmaz, IsDeleted = true yapılır.
/// - In-Memory filtering iptal: Tüm WHERE, COUNT, TAKE ve SKIP sorguları SQL üzerinde çalışır.
/// - IMemoryCache ile versiyon tabanlı önbellekleme kullanılır.
/// </summary>
public class NoteService : INoteService
{
    private readonly ApplicationDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly ILogger<NoteService> _logger;
    private const string NotesCacheTag = "cache_notes_version";

    public NoteService(
        ApplicationDbContext context,
        IMemoryCache cache,
        ILogger<NoteService> logger)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
    }

    public async Task<(List<Note> notes, int totalCount, int totalPages)> GetNotesPagedAsync(int page = 1, int pageSize = 15)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 50) pageSize = 15;

        var cacheVersion = GetCacheVersion();
        var cacheKey = $"notes_paged_{cacheVersion}_{page}_{pageSize}";

        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);

            // SQL seviyesinde IsDeleted filtresi ve Count
            var total = await _context.Notes
                .AsNoTracking()
                .Where(n => !n.IsDeleted)
                .CountAsync();

            var totalPages = (int)Math.Ceiling(total / (double)pageSize);
            if (totalPages < 1) totalPages = 1;

            // SQL seviyesinde Where, OrderBy, Skip, Take
            var items = await _context.Notes
                .AsNoTracking()
                .Where(n => !n.IsDeleted)
                .OrderByDescending(n => n.IsPinned)
                .ThenByDescending(n => n.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, total, totalPages);
        });
    }

    public async Task<Note?> GetByIdAsync(int id)
    {
        return await _context.Notes
            .FirstOrDefaultAsync(n => n.Id == id && !n.IsDeleted);
    }

    public async Task<(bool success, string message, Note? note)> CreateNoteAsync(
        string title, string content, bool isPinned, string userId, string authorName, bool isAdmin)
    {
        title = title?.Trim() ?? string.Empty;
        content = content?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(title))
            return (false, "Not başlığı boş bırakılamaz.", null);

        if (string.IsNullOrWhiteSpace(content))
            return (false, "Not içeriği boş bırakılamaz.", null);

        var words = CountWords(content);

        // Kural: Admin sınırsız yazabilir. Diğer izinli kullanıcılar için 280 kelime sınırı.
        if (!isAdmin && words > 280)
        {
            _logger.LogWarning("[NoteService] Kelime sınırı aşıldı. UserId={UserId}, Words={Words}", userId, words);
            return (false, $"İzin verilen kelime sınırını aştınız. Sizin için sınır 280 kelimedir (Girdiğiniz: {words} kelime).", null);
        }

        var note = new Note
        {
            Title = title,
            Content = content,
            UserId = userId,
            AuthorName = string.IsNullOrWhiteSpace(authorName) ? "Anonim" : authorName,
            IsAdminNote = isAdmin,
            IsPinned = isAdmin && isPinned, // Yalnızca admin not sabitleyebilir
            WordCount = words,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = null,
            IsDeleted = false
        };

        _context.Notes.Add(note);
        await _context.SaveChangesAsync();

        InvalidateCache();
        _logger.LogInformation("[NoteService] Yeni not oluşturuldu. NoteId={NoteId}, UserId={UserId}", note.Id, userId);
        return (true, "Notunuz başarıyla kaydedildi.", note);
    }

    public async Task<(bool success, string message)> UpdateNoteAsync(
        int id, string title, string content, bool isPinned, string userId, bool isAdmin)
    {
        var note = await _context.Notes.FirstOrDefaultAsync(n => n.Id == id && !n.IsDeleted);
        if (note == null)
            return (false, "Not bulunamadı.");

        // Yetki: Not sahibi veya Admin güncelleyebilir
        if (!isAdmin && note.UserId != userId)
        {
            _logger.LogWarning("[NoteService] Yetkisiz düzenleme girişimi. NoteId={NoteId}, UserId={UserId}", id, userId);
            return (false, "Bu notu düzenleme yetkiniz bulunmuyor.");
        }

        title = title?.Trim() ?? string.Empty;
        content = content?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(title))
            return (false, "Not başlığı boş bırakılamaz.");

        if (string.IsNullOrWhiteSpace(content))
            return (false, "Not içeriği boş bırakılamaz.");

        var words = CountWords(content);

        // Admin olmayanlar için 280 kelime sınırı
        if (!isAdmin && words > 280)
        {
            return (false, $"İzin verilen kelime sınırını aştınız. Sizin için sınır 280 kelimedir (Girdiğiniz: {words} kelime).");
        }

        note.Title = title;
        note.Content = content;
        note.WordCount = words;
        if (isAdmin)
        {
            note.IsPinned = isPinned;
        }
        note.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        InvalidateCache();

        _logger.LogInformation("[NoteService] Not güncellendi. NoteId={NoteId}, UserId={UserId}", id, userId);
        return (true, "Not başarıyla güncellendi.");
    }

    public async Task<(bool success, string message)> DeleteNoteAsync(int id, string userId, bool isAdmin)
    {
        var note = await _context.Notes.FirstOrDefaultAsync(n => n.Id == id && !n.IsDeleted);
        if (note == null)
            return (false, "Not bulunamadı.");

        // Yetki: Not sahibi veya Admin silebilir
        if (!isAdmin && note.UserId != userId)
        {
            _logger.LogWarning("[NoteService] Yetkisiz silme girişimi. NoteId={NoteId}, UserId={UserId}", id, userId);
            return (false, "Bu notu silme yetkiniz bulunmuyor.");
        }

        // Soft-delete: Fiziksel silme yerine IsDeleted = true yap
        note.IsDeleted = true;
        note.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        InvalidateCache();

        _logger.LogInformation("[NoteService] Not soft-delete uygulandı. NoteId={NoteId}, UserId={UserId}", id, userId);
        return (true, "Not silindi.");
    }

    public async Task<int> GetTotalCountAsync()
    {
        return await _context.Notes
            .AsNoTracking()
            .Where(n => !n.IsDeleted)
            .CountAsync();
    }

    public int CountWords(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        return text.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
    }

    private int GetCacheVersion()
    {
        return _cache.GetOrCreate(NotesCacheTag, entry =>
        {
            entry.Priority = CacheItemPriority.NeverRemove;
            return 1;
        });
    }

    private void InvalidateCache()
    {
        var current = GetCacheVersion();
        _cache.Set(NotesCacheTag, current + 1);
    }
}
