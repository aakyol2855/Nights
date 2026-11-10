using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using Nights.Models.ViewModels;
using Nights.Services.Interfaces;

namespace Nights.Controllers;

public class MemoriesController : Controller
{
    private readonly IMemoryService _memoryService;
    private readonly IImageService _imageService;
    private readonly ILogger<MemoriesController> _logger;

    public MemoriesController(
        IMemoryService memoryService,
        IImageService imageService,
        ILogger<MemoriesController> logger)
    {
        _memoryService = memoryService;
        _imageService = imageService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        _logger.LogInformation("[MemoriesController] Anılar sayfası görüntülendi.");
        var memories = await _memoryService.GetApprovedMemoriesAsync();
        return View(memories);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("upload-policy")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<IActionResult> Submit([FromForm] MemorySubmitInputModel model)
    {
        if (!ModelState.IsValid)
        {
            var firstError = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .FirstOrDefault() ?? "Geçersiz form verisi.";
            return Json(new { success = false, message = firstError });
        }

        if (model.File == null || model.File.Length == 0)
        {
            return Json(new { success = false, message = "Lütfen bir resim dosyası seçiniz." });
        }

        try
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            await _memoryService.AddPendingMemoryAsync(model.Description, model.File, ip);

            _logger.LogInformation("[MemoriesController] Yeni anı gönderimi başarılı. IP: {Ip}", ip);
            return Json(new
            {
                success = true,
                message = "Anınız başarıyla alındı. Yönetici onayının ardından burada yayınlanacaktır."
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "[MemoriesController] Doğrulama hatası oluştu.");
            return Json(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MemoriesController] Anı yüklenirken beklenmeyen sunucu hatası oluştu.");
            return Json(new { success = false, message = "Dosya işlenirken bir sunucu hatası oluştu." });
        }
    }

    [HttpGet]
    [ResponseCache(Duration = 604800, Location = ResponseCacheLocation.Client)]
    public IActionResult Image(string f)
    {
        if (string.IsNullOrWhiteSpace(f)) return NotFound();

        var image = _imageService.GetImageStream(f, isPending: false);
        if (image == null) return NotFound();

        return File(image.Value.fileBytes, image.Value.contentType);
    }
}
