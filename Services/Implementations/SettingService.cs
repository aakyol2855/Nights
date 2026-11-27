using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Nights.Data;
using Nights.Models.Entities;
using Nights.Services.Interfaces;

namespace Nights.Services.Implementations;

public class SettingService : ISettingService
{
    private readonly ApplicationDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly ILogger<SettingService> _logger;
    private const string SettingsCacheKey = "divan_site_settings_cache";

    public SettingService(
        ApplicationDbContext context,
        IMemoryCache cache,
        ILogger<SettingService> logger)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
    }

    public async Task<SiteSetting> GetSettingsAsync()
    {
        return await _cache.GetOrCreateAsync(SettingsCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(2);

            var setting = await _context.SiteSettings.AsNoTracking().FirstOrDefaultAsync();
            if (setting == null)
            {
                setting = new SiteSetting();
                _context.SiteSettings.Add(setting);
                await _context.SaveChangesAsync();
            }
            return setting;
        }) ?? new SiteSetting();
    }

    public async Task UpdateSettingsAsync(SiteSetting setting)
    {
        var existing = await _context.SiteSettings.FirstOrDefaultAsync();
        if (existing == null)
        {
            setting.UpdatedAt = DateTime.UtcNow;
            _context.SiteSettings.Add(setting);
        }
        else
        {
            existing.GithubUrl = setting.GithubUrl?.Trim() ?? string.Empty;
            existing.LinkedinUrl = setting.LinkedinUrl?.Trim() ?? string.Empty;
            existing.ContactEmail = setting.ContactEmail?.Trim() ?? string.Empty;
            existing.QuoteText = setting.QuoteText?.Trim() ?? string.Empty;
            existing.QuoteAuthor = setting.QuoteAuthor?.Trim() ?? string.Empty;
            existing.LogoText = string.IsNullOrWhiteSpace(setting.LogoText) ? "DİVAN" : setting.LogoText.Trim();
            existing.LogoSubText = string.IsNullOrWhiteSpace(setting.LogoSubText) ? "PERSONAL DIGITAL ARCHIVE" : setting.LogoSubText.Trim();
            existing.CustomLogoUrl = setting.CustomLogoUrl?.Trim();
            existing.BackgroundMode = string.Equals(setting.BackgroundMode, "color", StringComparison.OrdinalIgnoreCase) ? "color" : "image";
            existing.DarkBackgroundImageUrl = setting.DarkBackgroundImageUrl?.Trim();
            existing.LightBackgroundImageUrl = setting.LightBackgroundImageUrl?.Trim();
            existing.DarkBackgroundColor = string.IsNullOrWhiteSpace(setting.DarkBackgroundColor) ? "#080D14" : setting.DarkBackgroundColor.Trim();
            existing.LightBackgroundColor = string.IsNullOrWhiteSpace(setting.LightBackgroundColor) ? "#F8F5EE" : setting.LightBackgroundColor.Trim();
            existing.BackgroundBlur = Math.Clamp(setting.BackgroundBlur, 0, 30);
            existing.BackgroundOpacity = Math.Clamp(setting.BackgroundOpacity, 10, 100);
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        _cache.Remove(SettingsCacheKey);
        _logger.LogInformation("[SettingService] Site ayarları güncellendi ve önbellek temizlendi.");
    }
}
