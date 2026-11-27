namespace Nights.Models.Entities;

/// <summary>
/// Kayıtlı kullanıcıların oyun ve zorluk bazlı en iyi skorlarını tutan kalıcı varlık.
/// Misafir (anonim) oyuncuların skorları bu tabloya yazılmaz; yalnızca oturumda (ISession) tutulur.
/// </summary>
public class GameScore : ISoftDeletable
{
    public int Id { get; set; }

    public string GameSlug { get; set; } = string.Empty;
    public string Difficulty { get; set; } = "standard";

    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Liderlik tablosunda gösterilecek güvenli oyuncu adı (E-posta adresi asla saklanmaz/gösterilmez).
    /// </summary>
    public string PlayerDisplayName { get; set; } = string.Empty;

    public int Score { get; set; }
    public int DurationSeconds { get; set; }
    public int Mistakes { get; set; }

    public DateTime AchievedAtUtc { get; set; } = DateTime.UtcNow;

    // ISoftDeletable
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }
}
