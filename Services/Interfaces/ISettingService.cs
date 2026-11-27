using Nights.Models.Entities;

namespace Nights.Services.Interfaces;

public interface ISettingService
{
    Task<SiteSetting> GetSettingsAsync();
    Task UpdateSettingsAsync(SiteSetting setting);
}
