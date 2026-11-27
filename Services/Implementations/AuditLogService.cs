using Microsoft.EntityFrameworkCore;
using Nights.Data;
using Nights.Models.Entities;
using Nights.Services.Interfaces;

namespace Nights.Services.Implementations;

public sealed class AuditLogService : IAuditLogService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<AuditLogService> _logger;

    private static readonly List<string> DefaultCategories =
    [
        "MemoryApproved",
        "MemoryRejected",
        "ItemSoftDeleted",
        "ItemRestored",
        "ItemHardDeleted",
        "DemoStarted",
        "DemoStopped",
        "AdminLoginSuccess",
        "AdminLoginFailed",
        "MaintenanceExecuted"
    ];

    public AuditLogService(
        IServiceScopeFactory scopeFactory,
        IHttpContextAccessor httpContextAccessor,
        ILogger<AuditLogService> logger)
    {
        _scopeFactory = scopeFactory;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task LogAsync(
        string category,
        string summary,
        string? targetEntityType = null,
        string? targetEntityId = null,
        string logLevel = "Information",
        string? username = null,
        string? ipAddress = null,
        string component = "System",
        CancellationToken ct = default)
    {
        try
        {
            var httpContext = _httpContextAccessor.HttpContext;
            var resolvedUser = string.IsNullOrWhiteSpace(username)
                ? (httpContext?.User?.Identity?.Name ?? "System")
                : username.Trim();

            var resolvedIp = string.IsNullOrWhiteSpace(ipAddress)
                ? (httpContext?.Connection?.RemoteIpAddress?.ToString() ?? "127.0.0.1")
                : ipAddress.Trim();

            var safeSummary = (summary ?? string.Empty).Trim();
            if (safeSummary.Length > 1000)
            {
                safeSummary = safeSummary[..1000];
            }

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var entry = new SystemAuditLog
            {
                TimestampUtc = DateTime.UtcNow,
                Category = string.IsNullOrWhiteSpace(category) ? "General" : category.Trim()[..Math.Min(category.Trim().Length, 80)],
                LogLevel = string.IsNullOrWhiteSpace(logLevel) ? "Information" : logLevel.Trim()[..Math.Min(logLevel.Trim().Length, 30)],
                Username = resolvedUser[..Math.Min(resolvedUser.Length, 150)],
                TargetEntityType = string.IsNullOrWhiteSpace(targetEntityType) ? null : targetEntityType.Trim()[..Math.Min(targetEntityType.Trim().Length, 60)],
                TargetEntityId = string.IsNullOrWhiteSpace(targetEntityId) ? null : targetEntityId.Trim()[..Math.Min(targetEntityId.Trim().Length, 80)],
                Summary = safeSummary,
                IpAddress = resolvedIp[..Math.Min(resolvedIp.Length, 64)],
                Component = string.IsNullOrWhiteSpace(component) ? "System" : component.Trim()[..Math.Min(component.Trim().Length, 80)]
            };

            db.SystemAuditLogs.Add(entry);
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // Loglama hatası ana iş akışını asla kesmemelidir
            _logger.LogWarning(ex, "[AuditLogService] Denetim kaydı yazılamadı. Category={Category}", category);
        }
    }

    public async Task<AuditLogPagedResultDto> GetPagedLogsAsync(AuditLogFilterDto filter, CancellationToken ct = default)
    {
        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 10, 100);

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var query = db.SystemAuditLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Category) && !filter.Category.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            var cat = filter.Category.Trim();
            query = query.Where(x => x.Category == cat);
        }

        if (!string.IsNullOrWhiteSpace(filter.LogLevel) && !filter.LogLevel.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            var lvl = filter.LogLevel.Trim();
            query = query.Where(x => x.LogLevel == lvl);
        }

        if (filter.FromDate.HasValue)
        {
            var fromUtc = DateTime.SpecifyKind(filter.FromDate.Value.Date, DateTimeKind.Utc);
            query = query.Where(x => x.TimestampUtc >= fromUtc);
        }

        if (filter.ToDate.HasValue)
        {
            var toExclusiveUtc = DateTime.SpecifyKind(filter.ToDate.Value.Date.AddDays(1), DateTimeKind.Utc);
            query = query.Where(x => x.TimestampUtc < toExclusiveUtc);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            query = query.Where(x =>
                x.Summary.Contains(term) ||
                x.Username.Contains(term) ||
                x.Category.Contains(term) ||
                x.IpAddress.Contains(term) ||
                x.Component.Contains(term) ||
                (x.TargetEntityId != null && x.TargetEntityId.Contains(term)));
        }

        var totalCount = await query.CountAsync(ct);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        if (page > totalPages) page = totalPages;

        var items = await query
            .OrderByDescending(x => x.TimestampUtc)
            .ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new AuditLogPagedResultDto
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = totalPages,
            AvailableCategories = DefaultCategories
        };
    }

    public async Task<int> PurgeOlderThanDaysAsync(int retentionDays, CancellationToken ct = default)
    {
        var safeDays = Math.Clamp(retentionDays, 7, 365);
        var thresholdUtc = DateTime.UtcNow.AddDays(-safeDays);

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var deleted = await db.SystemAuditLogs
            .Where(x => x.TimestampUtc < thresholdUtc)
            .ExecuteDeleteAsync(ct);

        if (deleted > 0)
        {
            _logger.LogInformation("[AuditLogService] {Count} adet {Days} günden eski denetim kaydı budandı.", deleted, safeDays);
        }

        return deleted;
    }
}
