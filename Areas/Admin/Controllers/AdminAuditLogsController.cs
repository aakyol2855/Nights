using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nights.Services.Interfaces;

namespace Nights.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class AdminAuditLogsController : Controller
{
    private readonly IAuditLogService _auditLogService;

    public AdminAuditLogsController(IAuditLogService auditLogService)
    {
        _auditLogService = auditLogService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? category = null,
        string? level = null,
        string? search = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int page = 1,
        CancellationToken ct = default)
    {
        var filter = new AuditLogFilterDto
        {
            Category = category,
            LogLevel = level,
            Search = search,
            FromDate = fromDate,
            ToDate = toDate,
            Page = page,
            PageSize = 25
        };

        var result = await _auditLogService.GetPagedLogsAsync(filter, ct);
        ViewBag.Filter = filter;
        return View(result);
    }
}
