namespace Nights.Services.Games.Modules;

/// <summary>
/// 4 zorluk seviyeli (Kolay, Orta, Zor, İmkansız) %100 istemci taraflı Sudoku modülü.
/// </summary>
public sealed class SudokuGameModule : IGameModule
{
    public string Slug => "sudoku";
    public string Title => "Divan Sudoku";
    public string Description => "Tek çözümlü 9x9 klasik mantık bulmacası. Kalem (not) modu, satır/blok aydınlatması ve 4 farklı ustalık derecesi.";
    public string IconClass => "icon-sudoku";
    public string Category => "Zeka & Mantık";
    public bool IsActive => true;
    public int SortOrder => 1;

    public IReadOnlyList<GameDifficultyOption> Difficulties { get; } = new List<GameDifficultyOption>
    {
        new("easy", "Kolay", 1.00, "40 İpucu • Başlangıç"),
        new("medium", "Orta", 1.25, "33 İpucu • Dengeli"),
        new("hard", "Zor", 1.50, "27 İpucu • Üstad"),
        new("impossible", "İmkansız", 2.00, "23 İpucu • Divan Sınavı")
    };

    public string ScriptPath => "/js/games/sudoku-game.js";
    public string? StylePath => "/css/games/sudoku.css";
    public ScoreSortOrder SortOrderType => ScoreSortOrder.DescendingByScore;

    public bool ValidateSubmission(GameScoreSubmissionDto submission)
    {
        if (submission == null || string.IsNullOrWhiteSpace(submission.Difficulty))
            return false;

        var diff = Difficulties.FirstOrDefault(d =>
            string.Equals(d.Key, submission.Difficulty, StringComparison.OrdinalIgnoreCase));

        if (diff == null)
            return false;

        // Tamamlanan hücre kontrolü (9x9 = 81 hücre)
        if (submission.CompletedCells != 81)
            return false;

        // Hata sayısı mantık sınırı
        if (submission.Mistakes < 0 || submission.Mistakes > 60)
            return false;

        // Anti-cheat minimum ve maksimum süre sınırları (saniye)
        var minSeconds = diff.Key switch
        {
            "easy" => 15,
            "medium" => 25,
            "hard" => 35,
            "impossible" => 45,
            _ => 15
        };

        if (submission.DurationSeconds < minSeconds || submission.DurationSeconds > 14400)
            return false;

        return true;
    }

    public int CalculateVerifiedScore(GameScoreSubmissionDto submission)
    {
        var diff = Difficulties.First(d =>
            string.Equals(d.Key, submission.Difficulty, StringComparison.OrdinalIgnoreCase));

        var targetSeconds = diff.Key switch
        {
            "easy" => 360,
            "medium" => 600,
            "hard" => 900,
            "impossible" => 1500,
            _ => 360
        };

        var basePoints = (int)Math.Round(1000 * diff.Multiplier);
        var timeBonus = Math.Max(0, (int)Math.Round((targetSeconds - submission.DurationSeconds) * 1.5 * diff.Multiplier));
        var mistakePenalty = submission.Mistakes * 50;

        return Math.Max(100, basePoints + timeBonus - mistakePenalty);
    }
}
