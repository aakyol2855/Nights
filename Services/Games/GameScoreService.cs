using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Nights.Data;
using Nights.Models.Entities;

namespace Nights.Services.Games;

public interface IGameScoreService
{
    Task<GameScoreSubmitResultDto> SubmitScoreAsync(
        string gameSlug,
        GameScoreSubmissionDto submission,
        ClaimsPrincipal user,
        ISession session,
        CancellationToken ct = default);

    Task<IReadOnlyList<GameLeaderboardEntryDto>> GetGameLeaderboardAsync(
        string gameSlug,
        string? difficulty,
        ClaimsPrincipal user,
        ISession session,
        CancellationToken ct = default);

    Task<IReadOnlyList<GlobalTierEntryDto>> GetGlobalTopTierListAsync(
        ClaimsPrincipal user,
        ISession session,
        CancellationToken ct = default);
}

public sealed partial class GameScoreService : IGameScoreService
{
    private const string GuestSessionKey = "Divan.Oyunhane.GuestScores.v1";
    private const string CacheVersionKey = "Divan.Oyunhane.CacheVersion";

    // Grand Prix / Tier Puan Dağıtım Tablosu (1. -> 10. sıra)
    private static readonly int[] BaseTierPointsByRank = [100, 80, 65, 50, 40, 30, 25, 20, 15, 10];

    private readonly ApplicationDbContext _context;
    private readonly IGameRegistryService _registry;
    private readonly IMemoryCache _cache;
    private readonly ILogger<GameScoreService> _logger;

    [GeneratedRegex(@"[^\p{L}\p{N}\s._-]")]
    private static partial Regex UnsafeDisplayNameCharsRegex();

    public GameScoreService(
        ApplicationDbContext context,
        IGameRegistryService registry,
        IMemoryCache cache,
        ILogger<GameScoreService> logger)
    {
        _context = context;
        _registry = registry;
        _cache = cache;
        _logger = logger;
    }

    public async Task<GameScoreSubmitResultDto> SubmitScoreAsync(
        string gameSlug,
        GameScoreSubmissionDto submission,
        ClaimsPrincipal user,
        ISession session,
        CancellationToken ct = default)
    {
        var module = _registry.GetBySlug(gameSlug);
        if (module == null)
        {
            return new GameScoreSubmitResultDto
            {
                Success = false,
                Message = "İstenen oyun modülü bulunamadı veya aktif değil."
            };
        }

        if (!module.ValidateSubmission(submission))
        {
            _logger.LogWarning("[GameScoreService] Geçersiz skor gönderimi reddedildi. Game={Slug}, Diff={Diff}, Time={Sec}",
                module.Slug, submission?.Difficulty, submission?.DurationSeconds);
            return new GameScoreSubmitResultDto
            {
                Success = false,
                Message = "Skor doğrulaması başarısız oldu (geçersiz süre veya oyun verisi)."
            };
        }

        var normalizedDiff = module.Difficulties
            .First(d => string.Equals(d.Key, submission.Difficulty, StringComparison.OrdinalIgnoreCase));

        var verifiedScore = module.CalculateVerifiedScore(submission);
        var now = DateTime.UtcNow;

        var isAuthenticated = user.Identity?.IsAuthenticated == true;
        var userId = isAuthenticated ? user.FindFirstValue(ClaimTypes.NameIdentifier) : null;

        // 1. GİRİŞ YAPMIŞ KULLANICI: Veritabanında kalıcı sakla (En iyi skor kuralı)
        if (isAuthenticated && !string.IsNullOrWhiteSpace(userId))
        {
            var safeUserName = SanitizeRegisteredUserName(user.Identity?.Name);
            var existing = await _context.GameScores
                .FirstOrDefaultAsync(g =>
                    !g.IsDeleted &&
                    g.UserId == userId &&
                    g.GameSlug == module.Slug &&
                    g.Difficulty == normalizedDiff.Key, ct);

            bool isPersonalBest;
            if (existing == null)
            {
                isPersonalBest = true;
                var entity = new GameScore
                {
                    GameSlug = module.Slug,
                    Difficulty = normalizedDiff.Key,
                    UserId = userId,
                    PlayerDisplayName = safeUserName,
                    Score = verifiedScore,
                    DurationSeconds = submission.DurationSeconds,
                    Mistakes = submission.Mistakes,
                    AchievedAtUtc = now
                };
                _context.GameScores.Add(entity);
                await _context.SaveChangesAsync(ct);
                BumpCacheVersion();
            }
            else if (IsBetterPerformance(module.SortOrderType, verifiedScore, submission.DurationSeconds, existing.Score, existing.DurationSeconds))
            {
                isPersonalBest = true;
                existing.Score = verifiedScore;
                existing.DurationSeconds = submission.DurationSeconds;
                existing.Mistakes = submission.Mistakes;
                existing.PlayerDisplayName = safeUserName;
                existing.AchievedAtUtc = now;
                await _context.SaveChangesAsync(ct);
                BumpCacheVersion();
            }
            else
            {
                isPersonalBest = false;
            }

            var board = await GetGameLeaderboardAsync(module.Slug, normalizedDiff.Key, user, session, ct);
            var myEntry = board.FirstOrDefault(e => e.IsCurrentUser && !e.IsGuestSession);

            _logger.LogInformation("[GameScoreService] Kalıcı skor işlendi. User={User}, Game={Game}, Diff={Diff}, Score={Score}, Best={Best}",
                safeUserName, module.Slug, normalizedDiff.Key, verifiedScore, isPersonalBest);

            return new GameScoreSubmitResultDto
            {
                Success = true,
                FinalScore = verifiedScore,
                CurrentRank = myEntry?.Rank,
                EarnedTierPoints = myEntry?.TierPoints ?? 0,
                IsPersonalBest = isPersonalBest,
                IsSavedPermanently = true,
                IsGuestSession = false,
                DisplayName = safeUserName,
                Message = isPersonalBest
                    ? "Tebrikler! Yeni kişisel rekorunuz kalıcı olarak kaydedildi."
                    : "Oyun tamamlandı! Mevcut en iyi rekorunuz korundu."
            };
        }

        // 2. ANONİM / MİSAFİR ZİYARETÇİ: Yalnızca HttpContext.Session içinde tut (DB'ye yazılmaz)
        var guestName = SanitizeGuestDisplayName(submission.DisplayName);
        var guestScores = ReadGuestScores(session);

        var existingGuest = guestScores.FirstOrDefault(x =>
            string.Equals(x.GameSlug, module.Slug, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.Difficulty, normalizedDiff.Key, StringComparison.OrdinalIgnoreCase));

        bool guestPersonalBest;
        if (existingGuest == null)
        {
            guestPersonalBest = true;
            if (guestScores.Count >= 30)
            {
                guestScores.RemoveAt(0);
            }

            guestScores.Add(new GuestSessionScoreItem
            {
                GameSlug = module.Slug,
                Difficulty = normalizedDiff.Key,
                DisplayName = guestName,
                Score = verifiedScore,
                DurationSeconds = submission.DurationSeconds,
                Mistakes = submission.Mistakes,
                AchievedAtUtc = now
            });
        }
        else
        {
            existingGuest.DisplayName = guestName;
            if (IsBetterPerformance(module.SortOrderType, verifiedScore, submission.DurationSeconds, existingGuest.Score, existingGuest.DurationSeconds))
            {
                guestPersonalBest = true;
                existingGuest.Score = verifiedScore;
                existingGuest.DurationSeconds = submission.DurationSeconds;
                existingGuest.Mistakes = submission.Mistakes;
                existingGuest.AchievedAtUtc = now;
            }
            else
            {
                guestPersonalBest = false;
            }
        }

        // Aynı oturumdaki diğer kayıtlarda da takma adı senkronize et (tek misafir kimliği)
        foreach (var item in guestScores)
        {
            item.DisplayName = guestName;
        }

        WriteGuestScores(session, guestScores);

        var guestBoard = await GetGameLeaderboardAsync(module.Slug, normalizedDiff.Key, user, session, ct);
        var myGuestEntry = guestBoard.FirstOrDefault(e => e.IsGuestSession);

        return new GameScoreSubmitResultDto
        {
            Success = true,
            FinalScore = verifiedScore,
            CurrentRank = myGuestEntry?.Rank,
            EarnedTierPoints = myGuestEntry?.TierPoints ?? 0,
            IsPersonalBest = guestPersonalBest,
            IsSavedPermanently = false,
            IsGuestSession = true,
            DisplayName = guestName,
            Message = "Skorunuz bu misafir oturumu için sıralamaya işlendi."
        };
    }

    public async Task<IReadOnlyList<GameLeaderboardEntryDto>> GetGameLeaderboardAsync(
        string gameSlug,
        string? difficulty,
        ClaimsPrincipal user,
        ISession session,
        CancellationToken ct = default)
    {
        var module = _registry.GetBySlug(gameSlug);
        if (module == null)
            return Array.Empty<GameLeaderboardEntryDto>();

        var diffKey = string.IsNullOrWhiteSpace(difficulty) || difficulty.Equals("all", StringComparison.OrdinalIgnoreCase)
            ? "all"
            : difficulty.Trim().ToLowerInvariant();

        if (diffKey != "all" && !module.Difficulties.Any(d => string.Equals(d.Key, diffKey, StringComparison.OrdinalIgnoreCase)))
        {
            diffKey = "all";
        }

        var permanentCandidates = await GetCachedPermanentGameCandidatesAsync(module, diffKey, ct);
        var currentUserId = user.Identity?.IsAuthenticated == true
            ? user.FindFirstValue(ClaimTypes.NameIdentifier)
            : null;

        var guestScores = ReadGuestScores(session)
            .Where(g => string.Equals(g.GameSlug, module.Slug, StringComparison.OrdinalIgnoreCase) &&
                        (diffKey == "all" || string.Equals(g.Difficulty, diffKey, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        return BuildFinalGameTop10(module, permanentCandidates, guestScores, currentUserId);
    }

    public async Task<IReadOnlyList<GlobalTierEntryDto>> GetGlobalTopTierListAsync(
        ClaimsPrincipal user,
        ISession session,
        CancellationToken ct = default)
    {
        var currentUserId = user.Identity?.IsAuthenticated == true
            ? user.FindFirstValue(ClaimTypes.NameIdentifier)
            : null;

        var guestScores = ReadGuestScores(session);

        // Misafir skoru yoksa doğrudan IMemoryCache üzerindeki genel sıralamayı kullan (Sıfır ekstra hesaplama)
        if (guestScores.Count == 0)
        {
            var ver = GetCacheVersion();
            var cacheKey = $"Divan.Oyunhane.GlobalTier.v{ver}";

            var cached = await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30);
                return await ComputeGlobalTierListCoreAsync(Array.Empty<GuestSessionScoreItem>(), ct);
            }) ?? Array.Empty<GlobalTierEntryDto>();

            // IsCurrentUser bayrağını mevcut kullanıcıya göre işaretle
            var currentKey = !string.IsNullOrWhiteSpace(currentUserId) ? $"user:{currentUserId}" : string.Empty;
            return cached.Select(c => new GlobalTierEntryDto
            {
                Rank = c.Rank,
                PlayerKey = c.PlayerKey,
                PlayerDisplayName = c.PlayerDisplayName,
                TotalTierPoints = c.TotalTierPoints,
                GamesRankedCount = c.GamesRankedCount,
                BestRank = c.BestRank,
                TierTitle = c.TierTitle,
                IsGuestSession = c.IsGuestSession,
                IsCurrentUser = !string.IsNullOrEmpty(currentKey) && c.PlayerKey == currentKey
            }).ToList();
        }

        // Misafir oturumunda skor varsa, önbellekteki kalıcı oyun adaylarıyla oturum skorlarını bellekte birleştir
        var withGuest = await ComputeGlobalTierListCoreAsync(guestScores, ct);
        return withGuest.Select(c => new GlobalTierEntryDto
        {
            Rank = c.Rank,
            PlayerKey = c.PlayerKey,
            PlayerDisplayName = c.PlayerDisplayName,
            TotalTierPoints = c.TotalTierPoints,
            GamesRankedCount = c.GamesRankedCount,
            BestRank = c.BestRank,
            TierTitle = c.TierTitle,
            IsGuestSession = c.IsGuestSession,
            IsCurrentUser = c.IsGuestSession || (!string.IsNullOrWhiteSpace(currentUserId) && c.PlayerKey == $"user:{currentUserId}")
        }).ToList();
    }

    private async Task<IReadOnlyList<GlobalTierEntryDto>> ComputeGlobalTierListCoreAsync(
        IReadOnlyList<GuestSessionScoreItem> guestScores,
        CancellationToken ct)
    {
        var activeModules = _registry.GetActiveGames();
        var aggregates = new Dictionary<string, (string DisplayName, int TotalPoints, int GamesCount, int BestRank, bool IsGuest)>(StringComparer.Ordinal);

        foreach (var module in activeModules)
        {
            var permCandidates = await GetCachedPermanentGameCandidatesAsync(module, "all", ct);
            var moduleGuestScores = guestScores
                .Where(g => string.Equals(g.GameSlug, module.Slug, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var top10 = BuildFinalGameTop10(module, permCandidates, moduleGuestScores, null);

            foreach (var entry in top10)
            {
                if (!aggregates.TryGetValue(entry.PlayerKey, out var agg))
                {
                    aggregates[entry.PlayerKey] = (entry.PlayerDisplayName, entry.TierPoints, 1, entry.Rank, entry.IsGuestSession);
                }
                else
                {
                    aggregates[entry.PlayerKey] = (
                        entry.PlayerDisplayName,
                        agg.TotalPoints + entry.TierPoints,
                        agg.GamesCount + 1,
                        Math.Min(agg.BestRank, entry.Rank),
                        agg.IsGuest
                    );
                }
            }
        }

        var ordered = aggregates
            .OrderByDescending(kv => kv.Value.TotalPoints)
            .ThenBy(kv => kv.Value.BestRank)
            .ThenByDescending(kv => kv.Value.GamesCount)
            .Take(10)
            .ToList();

        var result = new List<GlobalTierEntryDto>(ordered.Count);
        for (var i = 0; i < ordered.Count; i++)
        {
            var rank = i + 1;
            var val = ordered[i].Value;
            result.Add(new GlobalTierEntryDto
            {
                Rank = rank,
                PlayerKey = ordered[i].Key,
                PlayerDisplayName = val.DisplayName,
                TotalTierPoints = val.TotalPoints,
                GamesRankedCount = val.GamesCount,
                BestRank = val.BestRank,
                TierTitle = GetOttomanTierTitle(rank),
                IsGuestSession = val.IsGuest
            });
        }

        return result;
    }

    private sealed record PermanentScoreProjection(
        string UserId,
        string PlayerDisplayName,
        string Difficulty,
        int Score,
        int DurationSeconds,
        int Mistakes,
        DateTime AchievedAtUtc);

    private async Task<IReadOnlyList<PermanentScoreProjection>> GetCachedPermanentGameCandidatesAsync(
        IGameModule module,
        string diffKey,
        CancellationToken ct)
    {
        var ver = GetCacheVersion();
        var cacheKey = $"Divan.Oyunhane.PermBoard.{module.Slug}.{diffKey}.v{ver}";

        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30);

            IQueryable<GameScore> query = _context.GameScores
                .AsNoTracking()
                .Where(g => !g.IsDeleted && g.GameSlug == module.Slug);

            if (diffKey != "all")
            {
                query = query.Where(g => g.Difficulty == diffKey);
            }

            query = module.SortOrderType == ScoreSortOrder.DescendingByScore
                ? query.OrderByDescending(g => g.Score).ThenBy(g => g.DurationSeconds).ThenBy(g => g.AchievedAtUtc)
                : query.OrderBy(g => g.DurationSeconds).ThenByDescending(g => g.Score).ThenBy(g => g.AchievedAtUtc);

            // DTO Projeksiyonu & SQL Limit (Kullanıcı başına en iyi kaydı seçebilmek için ilk 60 aday)
            var rawRows = await query
                .Take(60)
                .Select(g => new PermanentScoreProjection(
                    g.UserId,
                    g.PlayerDisplayName,
                    g.Difficulty,
                    g.Score,
                    g.DurationSeconds,
                    g.Mistakes,
                    g.AchievedAtUtc))
                .ToListAsync(ct);

            // "all" modunda aynı kullanıcının birden fazla zorlukta skoru varsa yalnızca en iyi tek derecesini al
            return rawRows
                .GroupBy(r => r.UserId, StringComparer.Ordinal)
                .Select(grp => grp.First())
                .Take(20)
                .ToList();
        }) ?? new List<PermanentScoreProjection>();
    }

    private static IReadOnlyList<GameLeaderboardEntryDto> BuildFinalGameTop10(
        IGameModule module,
        IReadOnlyList<PermanentScoreProjection> permanentCandidates,
        IReadOnlyList<GuestSessionScoreItem> guestCandidates,
        string? currentUserId)
    {
        var diffLookup = module.Difficulties.ToDictionary(d => d.Key, d => d, StringComparer.OrdinalIgnoreCase);

        var combined = new List<GameLeaderboardEntryDto>(permanentCandidates.Count + 1);

        foreach (var p in permanentCandidates)
        {
            var diffOpt = diffLookup.GetValueOrDefault(p.Difficulty);
            combined.Add(new GameLeaderboardEntryDto
            {
                PlayerKey = $"user:{p.UserId}",
                PlayerDisplayName = SanitizeRegisteredUserName(p.PlayerDisplayName),
                Difficulty = p.Difficulty,
                DifficultyLabel = diffOpt?.Label ?? p.Difficulty,
                Score = p.Score,
                DurationSeconds = p.DurationSeconds,
                Mistakes = p.Mistakes,
                IsGuestSession = false,
                IsCurrentUser = !string.IsNullOrWhiteSpace(currentUserId) && p.UserId == currentUserId,
                AchievedAtUtc = p.AchievedAtUtc
            });
        }

        if (guestCandidates.Count > 0)
        {
            var bestGuest = module.SortOrderType == ScoreSortOrder.DescendingByScore
                ? guestCandidates.OrderByDescending(g => g.Score).ThenBy(g => g.DurationSeconds).First()
                : guestCandidates.OrderBy(g => g.DurationSeconds).ThenByDescending(g => g.Score).First();

            var diffOpt = diffLookup.GetValueOrDefault(bestGuest.Difficulty);
            combined.Add(new GameLeaderboardEntryDto
            {
                PlayerKey = "guest:current-session",
                PlayerDisplayName = SanitizeGuestDisplayName(bestGuest.DisplayName),
                Difficulty = bestGuest.Difficulty,
                DifficultyLabel = diffOpt?.Label ?? bestGuest.Difficulty,
                Score = bestGuest.Score,
                DurationSeconds = bestGuest.DurationSeconds,
                Mistakes = bestGuest.Mistakes,
                IsGuestSession = true,
                IsCurrentUser = true,
                AchievedAtUtc = bestGuest.AchievedAtUtc
            });
        }

        var sorted = (module.SortOrderType == ScoreSortOrder.DescendingByScore
                ? combined.OrderByDescending(x => x.Score).ThenBy(x => x.DurationSeconds).ThenBy(x => x.AchievedAtUtc)
                : combined.OrderBy(x => x.DurationSeconds).ThenByDescending(x => x.Score).ThenBy(x => x.AchievedAtUtc))
            .Take(10)
            .ToList();

        for (var i = 0; i < sorted.Count; i++)
        {
            var item = sorted[i];
            item.Rank = i + 1;

            var baseTier = BaseTierPointsByRank[i];
            var multiplier = diffLookup.GetValueOrDefault(item.Difficulty)?.Multiplier ?? 1.0;
            item.TierPoints = (int)Math.Round(baseTier * multiplier);
        }

        return sorted;
    }

    private static bool IsBetterPerformance(
        ScoreSortOrder sortOrder,
        int candidateScore,
        int candidateDuration,
        int existingScore,
        int existingDuration)
    {
        if (sortOrder == ScoreSortOrder.DescendingByScore)
        {
            if (candidateScore != existingScore)
                return candidateScore > existingScore;
            return candidateDuration < existingDuration;
        }

        if (candidateDuration != existingDuration)
            return candidateDuration < existingDuration;
        return candidateScore > existingScore;
    }

    private static string GetOttomanTierTitle(int rank) => rank switch
    {
        1 => "Başmimar",
        2 or 3 => "Üstad",
        <= 6 => "Kalfa",
        _ => "Ehliyetli"
    };

    /// <summary>
    /// E-posta adreslerinin liderlik tablosunda görünmesini kesin olarak engeller.
    /// </summary>
    public static string SanitizeRegisteredUserName(string? rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
            return "Divan Oyuncusu";

        var trimmed = rawName.Trim();
        var atIdx = trimmed.IndexOf('@');
        if (atIdx > 0)
        {
            trimmed = trimmed[..atIdx];
        }

        var clean = UnsafeDisplayNameCharsRegex().Replace(trimmed, string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(clean))
            return "Divan Oyuncusu";

        return clean.Length > 28 ? clean[..28] : clean;
    }

    public static string SanitizeGuestDisplayName(string? rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
            return "Misafir Oyuncu";

        var trimmed = rawName.Trim();
        var atIdx = trimmed.IndexOf('@');
        if (atIdx > 0)
        {
            trimmed = trimmed[..atIdx];
        }

        var clean = UnsafeDisplayNameCharsRegex().Replace(trimmed, string.Empty).Trim();
        if (clean.Length < 2)
            return "Misafir Oyuncu";

        return clean.Length > 24 ? clean[..24] : clean;
    }

    private static List<GuestSessionScoreItem> ReadGuestScores(ISession session)
    {
        try
        {
            var json = session.GetString(GuestSessionKey);
            if (string.IsNullOrWhiteSpace(json))
                return new List<GuestSessionScoreItem>();

            return JsonSerializer.Deserialize<List<GuestSessionScoreItem>>(json) ?? new List<GuestSessionScoreItem>();
        }
        catch
        {
            return new List<GuestSessionScoreItem>();
        }
    }

    private static void WriteGuestScores(ISession session, List<GuestSessionScoreItem> items)
    {
        var json = JsonSerializer.Serialize(items);
        session.SetString(GuestSessionKey, json);
    }

    private int GetCacheVersion()
    {
        return _cache.GetOrCreate(CacheVersionKey, entry =>
        {
            entry.Priority = CacheItemPriority.NeverRemove;
            return 1;
        });
    }

    private void BumpCacheVersion()
    {
        var next = GetCacheVersion() + 1;
        _cache.Set(CacheVersionKey, next, new MemoryCacheEntryOptions
        {
            Priority = CacheItemPriority.NeverRemove
        });
    }
}
