using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Nights.Services.Interfaces;

namespace Nights.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class AdminUsersController : Controller
{
    private readonly IUserService _userService;
    private readonly ILogger<AdminUsersController> _logger;

    public AdminUsersController(IUserService userService, ILogger<AdminUsersController> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        _logger.LogInformation("[AdminUsersController] Kullanıcı izinleri listesi talep edildi.");
        var list = await _userService.GetUsersWithPermissionsAsync();
        return View(list);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleNoteAccess(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Json(new { success = false, message = "Geçersiz kullanıcı kimliği." });
        }

        var (success, hasAccess, message) = await _userService.ToggleNoteAccessAsync(userId);
        return Json(new { success, hasAccess, message });
    }
}
