using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Nights.Data;
using Nights.Models.Entities;
using Nights.Services.Interfaces;

namespace Nights.Services.Implementations;

/// <summary>
/// Anı (Memory) yönetim servisi.
/// Performans & Mimari Kuralları:
/// - Tüm okuma sorguları: AsNoTracking + IQueryable filtresi (SQL tarafında WHERE/ORDER/TAKE).
/// - DTO/Projeksiyon: UI'a gönderilen anılarda hassas SubmitterIpHash çekilmez.
/// - Delete: Dosya sistemiyle birlikte kaldırılması gerektiğinden fiziksel silme yapılır.
/// </summary>
public class MemoryService : IMemoryService
{
    private readonly ApplicationDbContext _context;
    private readonly IImageService _imageService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<MemoryService> _logger;
    private const string ApprovedMemoriesCacheKey = "cache_approved_memories";

    public MemoryService(
        ApplicationDbContext context,
        IImageService imageService,
        IMemoryCache cache,
        ILogger<MemoryService> logger)
    {
        _context = context;
        _imageService = imageService;
        _cache = cache;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<List<Memory>> GetApprovedMemoriesAsync()
    {
        return await _cache.GetOrCreateAsync(ApprovedMemoriesCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30);

            // IQueryable: Tüm filtre SQL tarafında çalışır, AsNoTracking ve Projeksiyon ile IP gizlenir
            return await _context.Memories
                .AsNoTracking()
                .Where(m => m.IsApproved)
                .OrderByDescending(m => m.CreatedAt)
                .Take(60)
                .Select(m => new Memory
                {
                    Id = m.Id,
                    Description = m.Description,
                    ImageFileName = m.ImageFileName,
                    IsApproved = m.IsApproved,
                    CreatedAt = m.CreatedAt,
                    SubmitterIpHash = string.Empty // Güvenlik & Projeksiyon: UI'a sızdırılmaz
                })
                .ToListAsync();
        }) ?? new List<Memory>();
    }

    /// <inheritdoc/>
    public async Task<List<Memory>> GetPendingMemoriesAsync()
    {
        return await _context.Memories
            .AsNoTracking()
            .Where(m => !m.IsApproved)
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync();
    }

    /// <inheritdoc/>
    /// <remarks>
    /// SQL tarafında TOP uygulanır. Dashboard'da in-memory .Take() kullanılmaz.
    /// </remarks>
    public async Task<List<Memory>> GetRecentPendingAsync(int count)
    {
        if (count < 1) count = 5;

        return await _context.Memories
            .AsNoTracking()
            .Where(m => !m.IsApproved)
            .OrderByDescending(m => m.CreatedAt)
            .Take(count)
            .ToListAsync();
    }

    /// <inheritdoc/>
    public async Task<Memory?> GetByIdAsync(int id)
    {
        return await _context.Memories.FindAsync(id);
    }

    /// <inheritdoc/>
    public async Task AddPendingMemoryAsync(string description, IFormFile file, string ipAddress)
    {
        var safeFileName = await _imageService.ProcessAndSavePendingAsync(file);

        var ipHash = string.IsNullOrEmpty(ipAddress)
            ? "unknown"
            : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ipAddress)));

        var memory = new Memory
        {
            Description = description.Trim(),
            ImageFileName = safeFileName,
            IsApproved = false,
            CreatedAt = DateTime.UtcNow,
            SubmitterIpHash = ipHash
        };

        _context.Memories.Add(memory);
        await _context.SaveChangesAsync();

        _logger.LogInformation("[MemoryService] Yeni anı beklemeye alındı. FileName={FileName}", safeFileName);
    }

    /// <inheritdoc/>
    public async Task<bool> ApproveAsync(int id)
    {
        var memory = await _context.Memories.FindAsync(id);
        if (memory == null) return false;

        var moved = await _imageService.ApproveImageAsync(memory.ImageFileName);
        if (!moved)
        {
            _logger.LogWarning("[MemoryService] Görsel taşıma başarısız. MemoryId={Id}, FileName={FileName}", id, memory.ImageFileName);
            return false;
        }

        memory.IsApproved = true;
        await _context.SaveChangesAsync();

        _cache.Remove(ApprovedMemoriesCacheKey);
        _logger.LogInformation("[MemoryService] Anı onaylandı. MemoryId={Id}", id);
        return true;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Hard-delete: Görsel dosyası da fiziksel olarak kaldırılır.
    /// Anılar için soft-delete uygulanmaz; onay bekleyen reddetmede dosya da silinmeli.
    /// </remarks>
    public async Task<bool> DeleteAsync(int id)
    {
        var memory = await _context.Memories.FindAsync(id);
        if (memory == null) return false;

        _imageService.DeleteImage(memory.ImageFileName, isPending: !memory.IsApproved);
        _context.Memories.Remove(memory);
        await _context.SaveChangesAsync();

        if (memory.IsApproved)
        {
            _cache.Remove(ApprovedMemoriesCacheKey);
        }

        _logger.LogInformation("[MemoryService] Anı ve görsel silindi. MemoryId={Id}", id);
        return true;
    }

    /// <inheritdoc/>
    public async Task<(int pendingCount, int approvedCount)> GetCountsAsync()
    {
        var pending = await _context.Memories
            .AsNoTracking()
            .Where(m => !m.IsApproved)
            .CountAsync();

        var approved = await _context.Memories
            .AsNoTracking()
            .Where(m => m.IsApproved)
            .CountAsync();

        return (pending, approved);
    }
}
