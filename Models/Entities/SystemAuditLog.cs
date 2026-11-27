namespace Nights.Models.Entities;

/// <summary>
/// Sistem ve Güvenlik Denetim Günlüğü kaydı.
/// Kritik admin işlemleri, demo orkestrasyonu, güvenlik ve bakım faaliyetlerini izler.
/// </summary>
public class SystemAuditLog
{
    public int Id { get; set; }
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// İşlem kategorisi: MemoryApproved, MemoryRejected, ItemSoftDeleted, ItemRestored,
    /// ItemHardDeleted, DemoStarted, DemoStopped, AdminLoginSuccess, AdminLoginFailed, MaintenanceExecuted
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Seviye: Information, Warning, Security, Error
    /// </summary>
    public string LogLevel { get; set; } = "Information";

    public string Username { get; set; } = "System";
    public string? TargetEntityType { get; set; }
    public string? TargetEntityId { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string IpAddress { get; set; } = "127.0.0.1";
    public string Component { get; set; } = "System";
}
