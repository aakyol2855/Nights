using Nights.Models.Entities;

namespace Nights.Services.Interfaces;

public interface IMemoryService
{
    /// <summary>Onaylanmış anıları döndürür (cache'li, en fazla 60).</summary>
    Task<List<Memory>> GetApprovedMemoriesAsync();

    /// <summary>Onay bekleyen tüm anıları döndürür (en yeni önce).</summary>
    Task<List<Memory>> GetPendingMemoriesAsync();

    /// <summary>
    /// Dashboard'da gösterilmek üzere en son N adet onay bekleyen anıyı döndürür.
    /// SQL tarafında TOP/TAKE uygulanır — in-memory filtering yok.
    /// </summary>
    Task<List<Memory>> GetRecentPendingAsync(int count);

    Task<Memory?> GetByIdAsync(int id);
    Task AddPendingMemoryAsync(string description, IFormFile file, string ipAddress);
    Task<bool> ApproveAsync(int id);
    Task<bool> DeleteAsync(int id, string? deletedBy = null);
    Task<(int pendingCount, int approvedCount)> GetCountsAsync();
}
