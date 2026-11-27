using Nights.Models.Entities;

namespace Nights.Services.Interfaces;

public sealed class AuditLogFilterDto
{
    public string? Category { get; set; }
    public string? LogLevel { get; set; }
    public string? Search { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}

public sealed class AuditLogPagedResultDto
{
    public List<SystemAuditLog> Items { get; init; } = new();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages { get; init; }
    public List<string> AvailableCategories { get; init; } = new();
}

/// <summary>
/// Merkezi Sistem ve Güvenlik Denetim Günlüğü servisi.
/// </summary>
public interface IAuditLogService
{
    Task LogAsync(
        string category,
        string summary,
        string? targetEntityType = null,
        string? targetEntityId = null,
        string logLevel = "Information",
        string? username = null,
        string? ipAddress = null,
        string component = "System",
        CancellationToken ct = default);

    Task<AuditLogPagedResultDto> GetPagedLogsAsync(AuditLogFilterDto filter, CancellationToken ct = default);
    Task<int> PurgeOlderThanDaysAsync(int retentionDays, CancellationToken ct = default);
}
