using Nights.Services.Games;

namespace Nights.Models.ViewModels;

public sealed class OyunhaneIndexViewModel
{
    public IReadOnlyList<IGameModule> Games { get; init; } = Array.Empty<IGameModule>();
    public IReadOnlyList<GlobalTierEntryDto> GlobalTopTier { get; init; } = Array.Empty<GlobalTierEntryDto>();
    public bool IsAuthenticated { get; init; }
}

public sealed class OyunhanePlayViewModel
{
    public IGameModule Game { get; init; } = null!;
    public IReadOnlyList<GameLeaderboardEntryDto> InitialLeaderboard { get; init; } = Array.Empty<GameLeaderboardEntryDto>();
    public bool IsAuthenticated { get; init; }
    public string SafeCurrentUserName { get; init; } = string.Empty;
}
