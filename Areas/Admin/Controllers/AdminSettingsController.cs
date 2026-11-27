using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Nights.Models.Entities;
using Nights.Services.Interfaces;

namespace Nights.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class AdminSettingsController : Controller
{
    private readonly ISettingService _settingService;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<AdminSettingsController> _logger;

    public AdminSettingsController(
        ISettingService settingService, 
        IWebHostEnvironment environment,
        ILogger<AdminSettingsController> logger)
    {
        _settingService = settingService;
        _environment = environment;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var settings = await _settingService.GetSettingsAsync();
        ViewBag.AvailableLogos = GetAvailableLogos();
        ViewBag.AvailableBackgrounds = GetAvailableBackgrounds();
        return View(settings);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(SiteSetting model)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.AvailableLogos = GetAvailableLogos();
            ViewBag.AvailableBackgrounds = GetAvailableBackgrounds();
            return View(model);
        }

        await _settingService.UpdateSettingsAsync(model);
        _logger.LogInformation("[AdminSettingsController] Site ayarları güncellendi.");
        TempData["SuccessMessage"] = "Site ayarları (arka plan, logo, sosyal linkler ve alıntı) başarıyla güncellendi.";
        return RedirectToAction(nameof(Index));
    }

    private List<string> GetAvailableLogos()
    {
        var list = new List<string>();
        try
        {
            var imagesPath = Path.Combine(_environment.WebRootPath, "images");
            if (Directory.Exists(imagesPath))
            {
                var files = Directory.GetFiles(imagesPath);
                var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    ".png", ".jpg", ".jpeg", ".svg", ".webp"
                };

                foreach (var file in files)
                {
                    var ext = Path.GetExtension(file);
                    var fileName = Path.GetFileName(file);
                    if (allowedExtensions.Contains(ext) && !fileName.Contains("hero-bg", StringComparison.OrdinalIgnoreCase))
                    {
                        list.Add(fileName);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Mevcut logo dosyaları taranırken hata oluştu.");
        }
        return list;
    }

    private List<string> GetAvailableBackgrounds()
    {
        var list = new List<string>();
        try
        {
            var imagesPath = Path.Combine(_environment.WebRootPath, "images");
            if (Directory.Exists(imagesPath))
            {
                var files = Directory.GetFiles(imagesPath);
                var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    ".png", ".jpg", ".jpeg", ".webp"
                };

                foreach (var file in files)
                {
                    var ext = Path.GetExtension(file);
                    var fileName = Path.GetFileName(file);
                    if (allowedExtensions.Contains(ext))
                    {
                        list.Add(fileName);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Mevcut arka plan dosyaları taranırken hata oluştu.");
        }
        return list;
    }
}
