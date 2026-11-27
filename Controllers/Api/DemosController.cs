using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Nights.Services.Interfaces;

namespace Nights.Controllers.Api;

[ApiController]
[Route("api/demos")]
public class DemosController : ControllerBase
{
    private readonly IDockerOrchestratorService _orchestrator;
    private readonly ITurnstileService _turnstileService;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<DemosController> _logger;

    public DemosController(
        IDockerOrchestratorService orchestrator,
        ITurnstileService turnstileService,
        IAuditLogService auditLogService,
        ILogger<DemosController> logger)
    {
        _orchestrator = orchestrator;
        _turnstileService = turnstileService;
        _auditLogService = auditLogService;
        _logger = logger;
    }

    public sealed class StartDemoRequest
    {
        public string? TurnstileToken { get; set; }
    }

    [HttpPost("start/{projectId:int}")]
    [EnableRateLimiting("demo-start-policy")]
    public async Task<IActionResult> Start(int projectId, [FromBody] StartDemoRequest? request, CancellationToken ct)
    {
        var isAdmin = User.Identity?.IsAuthenticated == true && User.IsInRole("Admin");

        // Ziyaretçiler için Cloudflare Turnstile doğrulaması (Admin muaf tutulur)
        if (!isAdmin)
        {
            var remoteIp = HttpContext.Connection.RemoteIpAddress?.ToString();
            var isHuman = await _turnstileService.VerifyTokenAsync(request?.TurnstileToken, remoteIp);
            if (!isHuman)
            {
                _logger.LogWarning("[DemosController] Turnstile doğrulaması başarısız. ProjectId={Id}, IP={Ip}", projectId, remoteIp);
                return BadRequest(new
                {
                    success = false,
                    message = "Güvenlik doğrulaması (Cloudflare Turnstile) tamamlanamadı. Lütfen doğrulamayı yapıp tekrar deneyin."
                });
            }
        }

        var result = await _orchestrator.StartDemoAsync(projectId, ct);

        if (result.IsBusy)
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new
            {
                success = false,
                isBusy = true,
                activeProjectTitle = result.ActiveProjectTitle,
                remainingSeconds = result.RemainingSeconds,
                message = result.Message
            });
        }

        if (!result.Success)
        {
            return BadRequest(new
            {
                success = false,
                isBusy = false,
                message = result.Message
            });
        }

        await _auditLogService.LogAsync(
            category: "DemoStarted",
            summary: $"Canlı demo başlatıldı: Proje #{projectId} (Slug: {result.Slug}).",
            targetEntityType: "Project",
            targetEntityId: projectId.ToString(),
            logLevel: "Information",
            username: User.Identity?.IsAuthenticated == true ? User.Identity.Name : "Ziyaretçi",
            component: "DemosController",
            ct: ct);

        return Ok(new
        {
            success = true,
            isBusy = false,
            demoUrl = result.DemoUrl,
            slug = result.Slug,
            remainingSeconds = result.RemainingSeconds,
            message = result.Message
        });
    }

    [HttpGet("status/{projectId:int}")]
    public IActionResult Status(int projectId)
    {
        var active = _orchestrator.GetCurrentActiveSession();
        if (active == null)
        {
            return Ok(new
            {
                hasActiveDemo = false,
                isThisProjectActive = false,
                remainingSeconds = 0
            });
        }

        var elapsed = DateTime.UtcNow - active.LastActivityUtc;
        var remainingSeconds = Math.Max(0, (int)(_orchestrator.IdleTimeout - elapsed).TotalSeconds);

        return Ok(new
        {
            hasActiveDemo = true,
            isThisProjectActive = active.ProjectId == projectId,
            activeProjectId = active.ProjectId,
            activeProjectTitle = active.ProjectTitle,
            slug = active.Slug,
            demoUrl = $"/live-demo/{active.Slug}/",
            remainingSeconds
        });
    }

    [HttpPost("stop")]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Stop(CancellationToken ct)
    {
        var stopped = await _orchestrator.StopActiveDemoAsync("AdminManualStop", ct);
        if (stopped)
        {
            await _auditLogService.LogAsync(
                category: "DemoStopped",
                summary: "Aktif canlı demo container yönetici tarafından manuel olarak durduruldu.",
                targetEntityType: "Container",
                logLevel: "Information",
                username: User.Identity?.Name,
                component: "DemosController",
                ct: ct);
        }
        return Ok(new
        {
            success = stopped,
            message = stopped ? "Aktif demo container durduruldu ve temizlendi." : "Şu anda çalışan bir demo bulunmuyor."
        });
    }
}
