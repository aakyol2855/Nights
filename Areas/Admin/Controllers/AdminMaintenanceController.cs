using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nights.Services.Interfaces;

namespace Nights.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class AdminMaintenanceController : Controller
{
    private readonly IMaintenanceService _maintenanceService;

    public AdminMaintenanceController(IMaintenanceService maintenanceService)
    {
        _maintenanceService = maintenanceService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct = default)
    {
        var diagnostics = await _maintenanceService.GetDiagnosticsAsync(ct);
        return View(diagnostics);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CleanOrphanFiles(CancellationToken ct = default)
    {
        var performedBy = User.Identity?.Name ?? "Admin";
        var (deletedCount, freedBytes) = await _maintenanceService.CleanOrphanFilesAsync(performedBy, ct);
        var freedMb = Math.Round(freedBytes / (1024.0 * 1024.0), 2);
        return Json(new
        {
            success = true,
            message = deletedCount > 0
                ? $"{deletedCount} yetim dosya silindi ({freedMb:0.##} MB alan kazanıldı)."
                : "Silinecek yetim dosya bulunamadı."
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CleanOrphanContainers(CancellationToken ct = default)
    {
        var performedBy = User.Identity?.Name ?? "Admin";
        var cleaned = await _maintenanceService.CleanOrphanContainersAsync(performedBy, ct);
        return Json(new
        {
            success = true,
            message = cleaned > 0
                ? $"{cleaned} adet yetim demo container başarıyla temizlendi."
                : "Temizlenecek yetim demo container bulunamadı."
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PurgeCache(CancellationToken ct = default)
    {
        var performedBy = User.Identity?.Name ?? "Admin";
        await _maintenanceService.PurgeMemoryCacheAsync(performedBy, ct);
        return Json(new
        {
            success = true,
            message = "Tüm IMemoryCache önbellek anahtarları başarıyla sıfırlandı."
        });
    }
}
