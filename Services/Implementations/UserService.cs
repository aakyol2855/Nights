using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nights.Models.ViewModels;
using Nights.Services.Interfaces;

namespace Nights.Services.Implementations;

/// <summary>
/// Kullanıcı ve yetki yönetim servisi implementasyonu.
/// </summary>
public class UserService : IUserService
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ILogger<UserService> _logger;

    private const string ContributorRole = "NoteContributor";
    private const string AdminRole = "Admin";

    public UserService(
        UserManager<IdentityUser> userManager,
        RoleManager<IdentityRole> roleManager,
        ILogger<UserService> logger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _logger = logger;
    }

    public async Task<List<UserPermissionViewModel>> GetUsersWithPermissionsAsync()
    {
        // AsNoTracking ile kullanıcı listesi çekilir
        var users = await _userManager.Users.AsNoTracking().ToListAsync();
        var list = new List<UserPermissionViewModel>(users.Count);

        foreach (var user in users)
        {
            var isUserAdmin = await _userManager.IsInRoleAsync(user, AdminRole);
            var hasNoteAccess = isUserAdmin || await _userManager.IsInRoleAsync(user, ContributorRole);

            list.Add(new UserPermissionViewModel
            {
                UserId = user.Id,
                UserName = user.UserName ?? "Kullanıcı",
                Email = user.Email ?? string.Empty,
                IsAdmin = isUserAdmin,
                HasNoteAccess = hasNoteAccess
            });
        }

        return list;
    }

    public async Task<(bool success, bool hasAccess, string message)> ToggleNoteAccessAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            _logger.LogWarning("[UserService] Yetki değiştirilmek istenen kullanıcı bulunamadı. UserId: {UserId}", userId);
            return (false, false, "Kullanıcı bulunamadı.");
        }

        if (await _userManager.IsInRoleAsync(user, AdminRole))
        {
            _logger.LogWarning("[UserService] Admin rolündeki kullanıcının yetkisi değiştirilemez. UserId: {UserId}", userId);
            return (false, true, "Admin kullanıcısının yetkileri değiştirilemez.");
        }

        if (!await _roleManager.RoleExistsAsync(ContributorRole))
        {
            await _roleManager.CreateAsync(new IdentityRole(ContributorRole));
            _logger.LogInformation("[UserService] {Role} rolü veritabanında oluşturuldu.", ContributorRole);
        }

        var isContributor = await _userManager.IsInRoleAsync(user, ContributorRole);
        IdentityResult result;

        if (isContributor)
        {
            result = await _userManager.RemoveFromRoleAsync(user, ContributorRole);
        }
        else
        {
            result = await _userManager.AddToRoleAsync(user, ContributorRole);
        }

        if (result.Succeeded)
        {
            var newState = !isContributor;
            _logger.LogInformation("[UserService] Kullanıcı yetkisi güncellendi. UserId: {UserId}, HasAccess: {HasAccess}", userId, newState);
            return (true, newState, newState ? "Kullanıcıya not defteri okuma/yazma izni verildi." : "Kullanıcının not defteri izni kaldırıldı.");
        }

        var errors = string.Join(", ", result.Errors.Select(e => e.Description));
        _logger.LogError("[UserService] Kullanıcı rolü güncellenirken hata oluştu: {Errors}", errors);
        return (false, isContributor, "Rol güncellenirken hata oluştu.");
    }
}
