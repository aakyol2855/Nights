using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Nights.Services.Interfaces;

namespace Nights.Areas.Identity.Pages.Account;

[EnableRateLimiting("auth-policy")]
[ValidateAntiForgeryToken]
public class RegisterModel : PageModel
{
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly IUserStore<IdentityUser> _userStore;
    private readonly ITurnstileService _turnstileService;
    private readonly ILogger<RegisterModel> _logger;

    public RegisterModel(
        UserManager<IdentityUser> userManager,
        IUserStore<IdentityUser> userStore,
        SignInManager<IdentityUser> signInManager,
        ITurnstileService turnstileService,
        ILogger<RegisterModel> logger)
    {
        _userManager = userManager;
        _userStore = userStore;
        _signInManager = signInManager;
        _turnstileService = turnstileService;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ReturnUrl { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "E-posta adresi gereklidir.")]
        [EmailAddress(ErrorMessage = "Lütfen geçerli bir e-posta adresi giriniz.")]
        [Display(Name = "E-posta Adresi")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Parola gereklidir.")]
        [StringLength(100, ErrorMessage = "{0} en az {2} ve en fazla {1} karakter uzunluğunda olmalıdır.", MinimumLength = 8)]
        [DataType(DataType.Password)]
        [Display(Name = "Parola")]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "Parola Tekrarı")]
        [Compare("Password", ErrorMessage = "Parola ve parola tekrarı eşleşmiyor.")]
        public string ConfirmPassword { get; set; } = string.Empty;

        // Honeypot alanı (Bot tuzağı)
        public string? WebsiteUrl { get; set; }
    }

    public void OnGet(string? returnUrl = null)
    {
        returnUrl ??= Url.Content("~/");
        if (!Url.IsLocalUrl(returnUrl))
        {
            returnUrl = Url.Content("~/");
        }
        ReturnUrl = returnUrl;
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        returnUrl ??= Url.Content("~/");
        if (!Url.IsLocalUrl(returnUrl))
        {
            returnUrl = Url.Content("~/");
        }
        ReturnUrl = returnUrl;

        // 1. Honeypot Bot Koruması
        if (!string.IsNullOrWhiteSpace(Input.WebsiteUrl))
        {
            _logger.LogWarning("[Auth-Security] Kayıt ekranında Honeypot bot girişimi engellendi. IP: {Ip}", HttpContext.Connection.RemoteIpAddress);
            ModelState.AddModelError(string.Empty, "Güvenlik doğrulaması başarısız oldu.");
            return Page();
        }

        // 2. Cloudflare Turnstile Bot Koruması
        var turnstileToken = Request.Form["cf-turnstile-response"].ToString();
        var remoteIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        var isTurnstileValid = await _turnstileService.VerifyTokenAsync(turnstileToken, remoteIp);
        if (!isTurnstileValid)
        {
            _logger.LogWarning("[Auth-Security] Kayıt ekranında Turnstile doğrulaması başarısız oldu. IP: {Ip}", remoteIp);
            ModelState.AddModelError(string.Empty, "Güvenlik doğrulaması (Turnstile) başarısız oldu veya süresi doldu. Lütfen tekrar deneyin.");
            return Page();
        }

        if (ModelState.IsValid)
        {
            var normalizedEmail = Input.Email.Trim();
            var user = new IdentityUser
            {
                UserName = normalizedEmail,
                Email = normalizedEmail,
                EmailConfirmed = true
            };

            await _userStore.SetUserNameAsync(user, normalizedEmail, CancellationToken.None);
            var result = await _userManager.CreateAsync(user, Input.Password);

            if (result.Succeeded)
            {
                _logger.LogInformation("[Auth-Security] Yeni kullanıcı hesabı oluşturuldu: {Email}, IP: {Ip}", normalizedEmail, remoteIp);
                await _signInManager.SignInAsync(user, isPersistent: false);
                return LocalRedirect(returnUrl);
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, TranslateIdentityError(error));
            }
        }

        return Page();
    }

    private static string TranslateIdentityError(IdentityError error)
    {
        return error.Code switch
        {
            "DuplicateUserName" or "DuplicateEmail" => "Bu e-posta adresi ile zaten bir kayıt mevcut.",
            "PasswordTooShort" => "Parola en az 8 karakter uzunluğunda olmalıdır.",
            "PasswordRequiresDigit" => "Parola en az bir rakam (0-9) içermelidir.",
            "PasswordRequiresLower" => "Parola en az bir küçük harf (a-z) içermelidir.",
            "PasswordRequiresUpper" => "Parola en az bir büyük harf (A-Z) içermelidir.",
            "PasswordRequiresNonAlphanumeric" => "Parola en az bir özel karakter (!@#$%^&* vb.) içermelidir.",
            "PasswordRequiresUniqueChars" => "Parola daha fazla benzersiz karakter içermelidir.",
            "InvalidEmail" => "Geçersiz e-posta adresi formatı.",
            _ => error.Description
        };
    }
}
