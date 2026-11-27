using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace Nights.Controllers;

/// <summary>
/// Navbar TR / EN dil seçici endpoint'i.
/// Seçilen kültürü 1 yıl geçerli .AspNetCore.Culture çerezine yazar ve Open-Redirect korumalı geri yönlendirme yapar.
/// </summary>
public class CultureController : Controller
{
    private static readonly HashSet<string> SupportedCultures = new(StringComparer.OrdinalIgnoreCase)
    {
        "tr-TR",
        "en-US"
    };

    [HttpPost("/set-language")]
    [ValidateAntiForgeryToken]
    public IActionResult SetLanguage(string culture, string? returnUrl = null)
    {
        var normalizedCulture = !string.IsNullOrWhiteSpace(culture) && SupportedCultures.Contains(culture.Trim())
            ? culture.Trim()
            : "tr-TR";

        // Kanonik büyük/küçük harf formu
        var canonicalCulture = normalizedCulture.Equals("en-US", StringComparison.OrdinalIgnoreCase)
            ? "en-US"
            : "tr-TR";

        Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(canonicalCulture)),
            new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                IsEssential = true,
                HttpOnly = false,
                SameSite = SameSiteMode.Lax,
                Secure = Request.IsHttps,
                Path = "/"
            });

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        return Redirect("/");
    }
}
