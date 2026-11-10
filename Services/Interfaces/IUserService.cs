using Nights.Models.ViewModels;

namespace Nights.Services.Interfaces;

/// <summary>
/// Kullanıcı ve rol yönetimi servisi.
/// Controller'ların UserManager/RoleManager ile doğrudan konuşmasını engeller.
/// </summary>
public interface IUserService
{
    Task<List<UserPermissionViewModel>> GetUsersWithPermissionsAsync();
    Task<(bool success, bool hasAccess, string message)> ToggleNoteAccessAsync(string userId);
}
