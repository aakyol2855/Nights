using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Nights.Areas.Identity.Pages.Account;

[ValidateAntiForgeryToken]
public class LogoutModel : PageModel
{
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly ILogger<LogoutModel> _logger;

    public LogoutModel(SignInManager<IdentityUser> signInManager, ILogger<LogoutModel> logger)
    {
        _signInManager = signInManager;
        _logger = logger;
    }

    public async Task<IActionResult> OnPost(string? returnUrl = null)
    {
        var userName = User.Identity?.Name;
        await _signInManager.SignOutAsync();
        _logger.LogInformation("[Auth-Security] Kullanıcı güvenli çıkış yaptı: {User}", userName);

        if (returnUrl != null && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        return RedirectToPage("/Index", new { area = "" });
    }
}
