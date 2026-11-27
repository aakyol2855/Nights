namespace Nights.Services.Games;

public interface IGameRegistryService
{
    IReadOnlyList<IGameModule> GetActiveGames();
    IReadOnlyList<IGameModule> GetAllGames();
    IGameModule? GetBySlug(string? slug);
}

/// <summary>
/// DI konteynerindeki tüm IGameModule implementasyonlarını otomatik toplayan kayıt defteri.
/// </summary>
public sealed class GameRegistryService : IGameRegistryService
{
    private readonly IReadOnlyList<IGameModule> _allModules;
    private readonly IReadOnlyList<IGameModule> _activeModules;
    private readonly Dictionary<string, IGameModule> _bySlug;

    public GameRegistryService(IEnumerable<IGameModule> modules)
    {
        _allModules = modules
            .OrderBy(m => m.SortOrder)
            .ThenBy(m => m.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _activeModules = _allModules
            .Where(m => m.IsActive)
            .ToList();

        _bySlug = _activeModules.ToDictionary(m => m.Slug, m => m, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<IGameModule> GetActiveGames() => _activeModules;

    public IReadOnlyList<IGameModule> GetAllGames() => _allModules;

    public IGameModule? GetBySlug(string? slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
            return null;

        return _bySlug.GetValueOrDefault(slug.Trim());
    }
}
