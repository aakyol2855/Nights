namespace Nights.Services.Games;

/// <summary>
/// Tak-çıkar (Plugin / Strategy) oyun modülü sözleşmesi.
/// Yeni bir oyun eklemek için yalnızca bu arayüzü implemente edip DI'a kaydetmek yeterlidir;
/// OyunhaneController, veritabanı şeması ve Top-Tier List sistemi otomatik uyum sağlar.
/// </summary>
public interface IGameModule
{
    string Slug { get; }
    string Title { get; }
    string Description { get; }
    string IconClass { get; }
    string Category { get; }
    bool IsActive { get; }
    int SortOrder { get; }

    IReadOnlyList<GameDifficultyOption> Difficulties { get; }

    string ScriptPath { get; }
    string? StylePath { get; }

    ScoreSortOrder SortOrderType { get; }

    /// <summary>
    /// Sunucu tarafında temel mantık ve anti-cheat doğrulaması yapar.
    /// </summary>
    bool ValidateSubmission(GameScoreSubmissionDto submission);

    /// <summary>
    /// İstemciden gelen veriye göre sunucu otoriteli nihai skoru hesaplar / sınırlar.
    /// </summary>
    int CalculateVerifiedScore(GameScoreSubmissionDto submission);
}
