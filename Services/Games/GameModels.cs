using System.ComponentModel.DataAnnotations;

namespace Nights.Services.Games;

public enum ScoreSortOrder
{
    DescendingByScore = 0,
    AscendingByTime = 1
}

public sealed record GameDifficultyOption(
    string Key,
    string Label,
    double Multiplier,
    string HintBadge);

public sealed class GameScoreSubmissionDto
{
    [Required]
    [StringLength(32)]
    public string Difficulty { get; set; } = "easy";

    public int Score { get; set; }
    public int DurationSeconds { get; set; }
    public int Mistakes { get; set; }
    public int CompletedCells { get; set; }

    /// <summary>
    /// Misafir oyuncuların geçici oturum boyunca kullanmak istediği takma ad.
    /// </summary>
    [StringLength(28)]
    public string? DisplayName { get; set; }
}

public sealed class GameLeaderboardEntryDto
{
    public int Rank { get; set; }
    public string PlayerKey { get; set; } = string.Empty;
    public string PlayerDisplayName { get; set; } = string.Empty;
    public string Difficulty { get; set; } = string.Empty;
    public string DifficultyLabel { get; set; } = string.Empty;
    public int Score { get; set; }
    public int DurationSeconds { get; set; }
    public int Mistakes { get; set; }
    public int TierPoints { get; set; }
    public bool IsGuestSession { get; set; }
    public bool IsCurrentUser { get; set; }
    public DateTime AchievedAtUtc { get; set; }
}

public sealed class GlobalTierEntryDto
{
    public int Rank { get; set; }
    public string PlayerKey { get; set; } = string.Empty;
    public string PlayerDisplayName { get; set; } = string.Empty;
    public int TotalTierPoints { get; set; }
    public int GamesRankedCount { get; set; }
    public int BestRank { get; set; }
    public string TierTitle { get; set; } = string.Empty;
    public bool IsGuestSession { get; set; }
    public bool IsCurrentUser { get; set; }
}

public sealed class GameScoreSubmitResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int FinalScore { get; set; }
    public int? CurrentRank { get; set; }
    public int EarnedTierPoints { get; set; }
    public bool IsPersonalBest { get; set; }
    public bool IsSavedPermanently { get; set; }
    public bool IsGuestSession { get; set; }
    public string DisplayName { get; set; } = string.Empty;
}

public sealed class GuestSessionScoreItem
{
    public string GameSlug { get; set; } = string.Empty;
    public string Difficulty { get; set; } = string.Empty;
    public string DisplayName { get; set; } = "Misafir Oyuncu";
    public int Score { get; set; }
    public int DurationSeconds { get; set; }
    public int Mistakes { get; set; }
    public DateTime AchievedAtUtc { get; set; }
}
