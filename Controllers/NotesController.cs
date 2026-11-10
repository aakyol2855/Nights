using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Nights.Models.ViewModels;
using Nights.Services.Interfaces;

namespace Nights.Controllers;

[Authorize]
public class NotesController : Controller
{
    private readonly INoteService _noteService;
    private readonly ILogger<NotesController> _logger;

    public NotesController(INoteService noteService, ILogger<NotesController> logger)
    {
        _noteService = noteService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int page = 1)
    {
        var isAdmin = User.IsInRole("Admin");
        var isContributor = User.IsInRole("NoteContributor");

        // Yalnızca Admin ve izin verilen NoteContributor rolündekiler erişebilir
        if (!isAdmin && !isContributor)
        {
            _logger.LogWarning("[NotesController] Yetkisiz erişim denemesi. User={User}", User.Identity?.Name);
            return View("AccessDeniedCustom");
        }

        var (notes, totalCount, totalPages) = await _noteService.GetNotesPagedAsync(page, 15);

        var viewModel = new NoteListViewModel
        {
            Notes = notes,
            CanCreate = true,
            IsAdmin = isAdmin,
            CurrentPage = page,
            TotalPages = totalPages,
            TotalCount = totalCount
        };

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromForm] NoteCreateInputModel model)
    {
        var isAdmin = User.IsInRole("Admin");
        var isContributor = User.IsInRole("NoteContributor");

        if (!isAdmin && !isContributor)
        {
            _logger.LogWarning("[NotesController] Yetkisiz not oluşturma denemesi. User={User}", User.Identity?.Name);
            return Json(new { success = false, message = "Not ekleme yetkiniz bulunmuyor." });
        }

        if (!ModelState.IsValid)
        {
            var firstError = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .FirstOrDefault() ?? "Geçersiz form verisi.";
            return Json(new { success = false, message = firstError });
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var authorName = User.Identity?.Name ?? "Misafir";

        var (success, message, note) = await _noteService.CreateNoteAsync(
            model.Title, model.Content, model.IsPinned, userId, authorName, isAdmin);

        return Json(new { success, message, noteId = note?.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit([FromForm] NoteEditInputModel model)
    {
        var isAdmin = User.IsInRole("Admin");
        var isContributor = User.IsInRole("NoteContributor");

        if (!isAdmin && !isContributor)
        {
            _logger.LogWarning("[NotesController] Yetkisiz not düzenleme denemesi. User={User}", User.Identity?.Name);
            return Json(new { success = false, message = "Yetkiniz bulunmuyor." });
        }

        if (!ModelState.IsValid)
        {
            return Json(new { success = false, message = "Geçersiz veri." });
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var (success, message) = await _noteService.UpdateNoteAsync(
            model.Id, model.Title, model.Content, model.IsPinned, userId, isAdmin);

        return Json(new { success, message });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var isAdmin = User.IsInRole("Admin");
        var isContributor = User.IsInRole("NoteContributor");

        if (!isAdmin && !isContributor)
        {
            _logger.LogWarning("[NotesController] Yetkisiz not silme denemesi. User={User}", User.Identity?.Name);
            return Json(new { success = false, message = "Yetkiniz bulunmuyor." });
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var (success, message) = await _noteService.DeleteNoteAsync(id, userId, isAdmin);

        return Json(new { success, message });
    }
}
