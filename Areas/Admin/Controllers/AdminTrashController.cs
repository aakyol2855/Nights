using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nights.Services.Interfaces;

namespace Nights.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class AdminTrashController : Controller
{
    private readonly ITrashService _trashService;
    private readonly IImageService _imageService;

    public AdminTrashController(ITrashService trashService, IImageService imageService)
    {
        _trashService = trashService;
        _imageService = imageService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? type = null, CancellationToken ct = default)
    {
        var overview = await _trashService.GetTrashOverviewAsync(type, ct);
        ViewBag.ActiveTypeFilter = string.IsNullOrWhiteSpace(type) ? "All" : type;
        return View(overview);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Restore(string entityType, int id, CancellationToken ct = default)
    {
        var performedBy = User.Identity?.Name ?? "Admin";
        var (success, message) = await _trashService.RestoreAsync(entityType, id, performedBy, ct);
        return Json(new { success, message });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> HardDelete(string entityType, int id, CancellationToken ct = default)
    {
        var performedBy = User.Identity?.Name ?? "Admin";
        var (success, message) = await _trashService.HardDeleteAsync(entityType, id, performedBy, ct);
        return Json(new { success, message });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PurgeExpired(CancellationToken ct = default)
    {
        var summary = await _trashService.PurgeExpiredAsync(ct);
        return Json(new
        {
            success = true,
            message = $"Süresi dolan kayıtlar tarandı: {summary.TotalEntitiesPurged} kayıt, {summary.FilesDeleted} dosya ve {summary.AuditLogsPruned} eski log temizlendi."
        });
    }

    [HttpGet]
    public IActionResult PreviewTrashImage(string f)
    {
        if (string.IsNullOrWhiteSpace(f)) return NotFound();

        var image = _imageService.GetTrashImageStream(f);
        if (image == null) return NotFound();

        return File(image.Value.fileBytes, image.Value.contentType);
    }
}
