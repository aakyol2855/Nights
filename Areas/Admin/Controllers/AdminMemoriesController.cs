using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nights.Services.Interfaces;

namespace Nights.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class AdminMemoriesController : Controller
{
    private readonly IMemoryService _memoryService;
    private readonly IImageService _imageService;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<AdminMemoriesController> _logger;

    public AdminMemoriesController(
        IMemoryService memoryService,
        IImageService imageService,
        IAuditLogService auditLogService,
        ILogger<AdminMemoriesController> logger)
    {
        _memoryService = memoryService;
        _imageService = imageService;
        _auditLogService = auditLogService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        _logger.LogInformation("[AdminMemoriesController] Bekleyen anılar listesi talep edildi.");
        var pending = await _memoryService.GetPendingMemoriesAsync();
        return View(pending);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id)
    {
        _logger.LogInformation("[AdminMemoriesController] Anı onaylama isteği. MemoryId={Id}", id);
        var success = await _memoryService.ApproveAsync(id);
        if (success)
        {
            await _auditLogService.LogAsync(
                category: "MemoryApproved",
                summary: $"Anı #{id} onaylandı ve yayına alındı.",
                targetEntityType: "Memory",
                targetEntityId: id.ToString(),
                logLevel: "Information",
                username: User.Identity?.Name,
                component: "AdminMemoriesController");
        }
        return Json(new { success, message = success ? "Anı onaylandı ve yayına alındı." : "İşlem başarısız oldu." });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id)
    {
        _logger.LogInformation("[AdminMemoriesController] Anı reddetme ve çöp kutusuna taşıma isteği. MemoryId={Id}", id);
        var success = await _memoryService.DeleteAsync(id, User.Identity?.Name);
        if (success)
        {
            await _auditLogService.LogAsync(
                category: "MemoryRejected",
                summary: $"Anı #{id} reddedildi ve Çöp Kutusu'na taşındı (30 gün saklama).",
                targetEntityType: "Memory",
                targetEntityId: id.ToString(),
                logLevel: "Warning",
                username: User.Identity?.Name,
                component: "AdminMemoriesController");
        }
        return Json(new { success, message = success ? "Anı reddedildi ve Çöp Kutusu'na taşındı." : "İşlem başarısız oldu." });
    }

    [HttpGet]
    public IActionResult PreviewPendingImage(string f)
    {
        if (string.IsNullOrWhiteSpace(f)) return NotFound();

        var image = _imageService.GetImageStream(f, isPending: true);
        if (image == null) return NotFound();

        return File(image.Value.fileBytes, image.Value.contentType);
    }
}
