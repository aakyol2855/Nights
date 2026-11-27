namespace Nights.Models.Entities;

/// <summary>
/// Çöp Kutusu (Soft-Delete) desteği olan varlıklar için ortak sözleşme.
/// </summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
    DateTime? DeletedAtUtc { get; set; }
    string? DeletedBy { get; set; }
}
